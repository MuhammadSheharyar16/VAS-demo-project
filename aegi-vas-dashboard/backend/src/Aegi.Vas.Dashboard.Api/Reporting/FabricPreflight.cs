using Aegi.Vas.Dashboard.Api.Contracts;

namespace Aegi.Vas.Dashboard.Api.Reporting;

/// <summary>
/// Connection check for the first run against a real Fabric endpoint:
///   dotnet Aegi.Vas.Dashboard.Api.dll --check-fabric
/// Signs in with the configured credential, runs every KPI query once and prints, per query, the row count
/// or the error Fabric returned (which names the missing view or column). Reads only; exits 0 when all pass.
/// </summary>
public static class FabricPreflight
{
    public static async Task<int> RunAsync(IServiceProvider services)
    {
        var source = ActivatorUtilities.CreateInstance<FabricSqlDataSource>(services);
        var failed = 0;

        async Task<T?> Check<T>(string name, Func<Task<T>> run, Func<T, string> describe)
        {
            try
            {
                var result = await run();
                Console.WriteLine($"  OK    {name,-22} {describe(result)}");
                return result;
            }
            catch (Exception ex)
            {
                failed++;
                var cause = ex.InnerException ?? ex;
                Console.WriteLine($"  FAIL  {name,-22} {cause.GetType().Name}: {cause.Message.ReplaceLineEndings(" ")}");
                return default;
            }
        }

        var ct = CancellationToken.None;
        Console.WriteLine("Fabric connection check");
        await Check("sign-in and connect", () => source.PingAsync(ct), ok => ok ? "connected" : "no result");
        if (failed > 0)
        {
            Console.WriteLine("Could not connect, so the queries were not run. Check Fabric:SqlEndpoint, Fabric:Database and the identity's workspace access.");
            return 1;
        }
        await Check("LastRefreshed.sql", () => source.GetLastRefreshedUtcAsync(ct), t => t is null ? "no refresh time recorded" : $"last refreshed {t:u}");
        var options = await Check("FilterOptions.sql", () => source.GetFilterOptionsAsync(ct),
            o => $"{o.Countries.Count} countries, {o.Countries.Sum(c => c.Centres.Count)} centres, {o.VisaTypes.Count} visa types, {o.MinMonth} to {o.MaxMonth}");

        // Year to date up to the latest month in the data, the same default the dashboard opens with.
        var to = options is { MaxMonth.Length: 7 } ? options.MaxMonth : DateTime.UtcNow.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
        var filter = new ReportFilter($"{to[..4]}-01", to, null, null, null);
        Console.WriteLine($"Queries for {filter.From} to {filter.To}, no other filters");

        static string Rows<T>(IReadOnlyList<T> rows) => rows.Count == 0 ? "0 rows (check the period and the view)" : $"{rows.Count} rows";
        await Check("Volume.sql", () => source.GetVolumeAsync(filter, ct), Rows);
        await Check("Outcomes.sql", () => source.GetOutcomesAsync(filter, ct), r => $"{Rows(r)}: {string.Join(", ", r.Select(x => x.Outcome))}");
        await Check("Closure.sql", () => source.GetClosureAsync(filter, ct), Rows);
        await Check("Stages.sql", () => source.GetStagesAsync(filter, ct), r => $"{Rows(r)}: {string.Join(", ", r.OrderBy(x => x.Order).Select(x => x.Stage).Distinct())}");
        await Check("Turnaround.sql digital", () => source.GetTurnaroundAsync(filter, "digital", ct), Rows);
        await Check("Turnaround.sql centre", () => source.GetTurnaroundAsync(filter, "centre", ct), Rows);
        await Check("Revenue.sql", () => source.GetRevenueAsync(filter, ct), Rows);
        await Check("VisaTypes.sql", () => source.GetVisaTypesAsync(filter, ct), Rows);
        await Check("Centres.sql", () => source.GetCentresAsync(filter, ct), r => $"{Rows(r)}, capacity recorded for {r.Count(x => x.Capacity is not null)}");

        Console.WriteLine(failed == 0 ? "All checks passed." : $"{failed} check(s) failed.");
        return failed == 0 ? 0 : 1;
    }
}
