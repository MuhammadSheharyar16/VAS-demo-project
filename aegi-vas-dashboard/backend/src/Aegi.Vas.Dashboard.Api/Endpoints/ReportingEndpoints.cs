using Aegi.Vas.Dashboard.Api.Configuration;
using Aegi.Vas.Dashboard.Api.Contracts;
using Aegi.Vas.Dashboard.Api.Reporting;
using Aegi.Vas.Dashboard.Api.Security;
using Aegi.Vas.Dashboard.Api.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Aegi.Vas.Dashboard.Api.Endpoints;

public static class ReportingEndpoints
{
    // Endpoint names are implementation choices (roadmap section 07), grouped by dashboard tab.
    public static void MapReportingEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1")
            .RequireAuthorization(AuthenticationSetup.DashboardAccessPolicy)
            .RequireRateLimiting("per-user")
            .WithTags("Reporting");

        api.MapGet("/filters", async (IReportingDataSource source, IMemoryCache cache, CancellationToken ct) =>
            Results.Ok(await FilterOptionsAsync(source, cache, ct)));

        api.MapGet("/executive-summary", (HttpContext http, KpiService svc, CancellationToken ct) =>
            Run(http, svc, (f, c) => svc.SummaryAsync(f, c), ct));

        var vas = api.MapGroup("/vas");
        vas.MapGet("/volume", (HttpContext http, KpiService svc, CancellationToken ct) => Run(http, svc, (f, c) => svc.VolumeAsync(f, c), ct));
        vas.MapGet("/outcomes", (HttpContext http, KpiService svc, CancellationToken ct) => Run(http, svc, (f, c) => svc.OutcomesAsync(f, c), ct));
        vas.MapGet("/closure", (HttpContext http, KpiService svc, CancellationToken ct) => Run(http, svc, (f, c) => svc.ClosureAsync(f, c), ct));
        vas.MapGet("/revenue", (HttpContext http, KpiService svc, CancellationToken ct) => Run(http, svc, (f, c) => svc.RevenueAsync(f, c), ct));
        vas.MapGet("/visa-types", (HttpContext http, KpiService svc, CancellationToken ct) => Run(http, svc, (f, c) => svc.VisaTypesAsync(f, c), ct));
        vas.MapGet("/turnaround", (HttpContext http, KpiService svc, string? journey, CancellationToken ct) =>
            journey is "digital" or "centre"
                ? Run(http, svc, (f, c) => svc.TurnaroundAsync(f, journey, c), ct)
                : Task.FromResult(ValidationProblem(new() { ["journey"] = ["Use 'digital' or 'centre'."] })));
    }

    private static async Task<IResult> Run<T>(HttpContext http, KpiService svc, Func<ReportFilter, CancellationToken, Task<T>> load, CancellationToken ct)
    {
        var q = http.Request.Query;
        var reporting = http.RequestServices.GetRequiredService<IOptions<ReportingSettings>>().Value;
        string? Opt(string key) => string.IsNullOrWhiteSpace(q[key]) ? null : q[key].ToString().Trim();
        var from = Opt("from") ?? reporting.DefaultFrom;
        var to = Opt("to") ?? reporting.DefaultTo;
        var country = Opt("country");
        var centre = Opt("centre");

        // Only look up the known countries/centres when a location filter was sent.
        var options = country is null && centre is null
            ? new FilterOptions("", "", [])
            : await FilterOptionsAsync(http.RequestServices.GetRequiredService<IReportingDataSource>(),
                http.RequestServices.GetRequiredService<IMemoryCache>(), ct);
        var errors = ReportFilterValidator.Validate(from, to, country, centre, options);
        if (errors.Count > 0) return ValidationProblem(errors);

        // Use canonical casing from the data so queries match exactly.
        var canonCountry = options.Countries.FirstOrDefault(c => c.Country.Equals(country, StringComparison.OrdinalIgnoreCase));
        var filter = new ReportFilter(from, to, canonCountry?.Country,
            canonCountry?.Centres.FirstOrDefault(c => c.Equals(centre, StringComparison.OrdinalIgnoreCase)));

        var data = await load(filter, ct);
        var meta = await svc.MetaAsync(filter, http.TraceIdentifier, ct);
        return Results.Ok(new ApiResponse<T>(data, meta));
    }

    private static IResult ValidationProblem(Dictionary<string, string[]> errors) =>
        Results.ValidationProblem(errors, title: "Some filters are not valid.");

    // Countries and centres change only with the gold refresh; a short cache avoids a query per filtered request.
    // Not permission-scoped: every authorised executive shares the same reporting scope (roadmap assumption).
    private static Task<FilterOptions> FilterOptionsAsync(IReportingDataSource source, IMemoryCache cache, CancellationToken ct) =>
        cache.GetOrCreateAsync("filter-options", e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
            return source.GetFilterOptionsAsync(ct);
        })!;
}
