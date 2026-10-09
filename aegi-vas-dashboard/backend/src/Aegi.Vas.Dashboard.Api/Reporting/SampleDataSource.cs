using Aegi.Vas.Dashboard.Api.Contracts;

namespace Aegi.Vas.Dashboard.Api.Reporting;

/// <summary>
/// Development-only stand-in for the Fabric gold views. Generates deterministic, illustrative rows at the
/// grain month x country x centre x journey x visa type and aggregates them the same way the SQL does.
/// Not AEGI data. Refused outside the Development environment.
/// </summary>
public sealed class SampleDataSource : IReportingDataSource
{
    private sealed record Fact(string Month, string Country, string Centre, string Journey, string VisaType,
        long Visas, long Approved, long Rejected, long Pending, long Withdrawn, long Closed, decimal TurnaroundDaysTotal, decimal Revenue);

    private static readonly (string Country, string Centre, double Weight, decimal FeeUsd)[] Centres =
    [
        ("UAE", "Dubai", 0.19, 66m), ("UAE", "Abu Dhabi", 0.12, 64m), ("Saudi Arabia", "Riyadh", 0.14, 61m),
        ("Saudi Arabia", "Jeddah", 0.09, 62m), ("Pakistan", "Islamabad", 0.10, 52m), ("Pakistan", "Karachi", 0.08, 51m),
        ("India", "Mumbai", 0.08, 55m), ("India", "New Delhi", 0.06, 56m), ("Egypt", "Cairo", 0.07, 58m),
        ("Philippines", "Manila", 0.04, 63m), ("Jordan", "Amman", 0.03, 60m),
    ];

    private static readonly (string Type, double Share, double Approve, double Reject, double Pending, double Close, double DigitalDays, double CentreDays)[] Types =
    [
        ("Tourist", 0.395, 0.899, 0.056, 0.034, 0.961, 1.6, 2.4), ("Visit", 0.226, 0.882, 0.069, 0.038, 0.954, 2.1, 3.0),
        ("Business", 0.146, 0.867, 0.074, 0.046, 0.942, 2.8, 3.7), ("Work", 0.117, 0.815, 0.108, 0.062, 0.910, 4.4, 6.1),
        ("Student", 0.066, 0.841, 0.093, 0.051, 0.926, 5.2, 6.8), ("Family", 0.050, 0.858, 0.081, 0.049, 0.933, 3.9, 5.3),
    ];

    private static readonly long[] MonthlyCentre = [21400, 20100, 23800, 22900, 24600, 27300, 26100, 22800, 21500];
    private static readonly long[] MonthlyDigital = [7200, 7600, 8900, 9400, 10200, 11800, 12300, 11500, 11900];
    // Appointment capacity across all centres per month, shared out by centre weight.
    private const double MonthlyCapacity = 27500;

    private static readonly Lazy<IReadOnlyList<Fact>> Facts = new(Build);

    public string Name => "Sample data";

    private static IReadOnlyList<Fact> Build()
    {
        var facts = new List<Fact>();
        var rng = new Random(20261007);
        for (var m = 0; m < MonthlyCentre.Length; m++)
        {
            var month = $"2026-{m + 1:00}";
            foreach (var (journey, monthTotal) in new[] { ("centre", MonthlyCentre[m]), ("digital", MonthlyDigital[m]) })
            foreach (var c in Centres)
            foreach (var t in Types)
            {
                var jitter = 0.9 + rng.NextDouble() * 0.2;
                var visas = (long)Math.Round(monthTotal * c.Weight * t.Share * jitter);
                if (visas == 0) continue;
                long Part(double rate) => (long)Math.Round(visas * rate * (0.97 + rng.NextDouble() * 0.06));
                var approved = Part(t.Approve);
                var rejected = Part(t.Reject);
                var pending = Math.Min(Part(t.Pending), Math.Max(0, visas - approved - rejected));
                var withdrawn = Math.Max(0, visas - approved - rejected - pending);
                var days = journey == "digital" ? t.DigitalDays : t.CentreDays;
                facts.Add(new Fact(month, c.Country, c.Centre, journey, t.Type, visas, approved, rejected, pending, withdrawn,
                    Math.Min(visas, Part(t.Close)), (decimal)(days * (0.92 + rng.NextDouble() * 0.16)) * visas, visas * c.FeeUsd));
            }
        }
        return facts;
    }

    private static IEnumerable<Fact> Where(ReportFilter f) => Facts.Value.Where(x =>
        string.CompareOrdinal(x.Month, f.From) >= 0 && string.CompareOrdinal(x.Month, f.To) <= 0 &&
        (f.Country is null || x.Country.Equals(f.Country, StringComparison.OrdinalIgnoreCase)) &&
        (f.Centre is null || x.Centre.Equals(f.Centre, StringComparison.OrdinalIgnoreCase)) &&
        (f.VisaType is null || x.VisaType.Equals(f.VisaType, StringComparison.OrdinalIgnoreCase)));

    private static Task<IReadOnlyList<T>> Done<T>(IEnumerable<T> rows) => Task.FromResult<IReadOnlyList<T>>(rows.ToList());

    public Task<IReadOnlyList<VolumeRow>> GetVolumeAsync(ReportFilter f, CancellationToken ct) =>
        Done(Where(f).GroupBy(x => (x.Month, x.Journey)).OrderBy(g => g.Key.Month)
            .Select(g => new VolumeRow(g.Key.Month, g.Key.Journey, g.Sum(x => x.Visas))));

    public Task<IReadOnlyList<OutcomeRow>> GetOutcomesAsync(ReportFilter f, CancellationToken ct)
    {
        var rows = Where(f).ToList();
        if (rows.Count == 0) return Done(Array.Empty<OutcomeRow>());
        return Done(new[]
        {
            new OutcomeRow("Approved", rows.Sum(x => x.Approved)), new OutcomeRow("Rejected", rows.Sum(x => x.Rejected)),
            new OutcomeRow("Pending", rows.Sum(x => x.Pending)), new OutcomeRow("Withdrawn", rows.Sum(x => x.Withdrawn)),
        });
    }

    public Task<IReadOnlyList<ClosureRow>> GetClosureAsync(ReportFilter f, CancellationToken ct) =>
        Done(Where(f).GroupBy(x => x.Month).OrderBy(g => g.Key)
            .Select(g => new ClosureRow(g.Key, g.Sum(x => x.Closed), g.Sum(x => x.Visas))));

    // Illustrative stages: each application counts in every stage it has reached.
    public Task<IReadOnlyList<StageRow>> GetStagesAsync(ReportFilter f, CancellationToken ct) =>
        Done(Where(f).GroupBy(x => x.Journey).OrderBy(g => g.Key).SelectMany(g =>
        {
            var decided = g.Sum(x => x.Visas - x.Pending);
            return new[]
            {
                new StageRow("Submitted", 1, g.Key, g.Sum(x => x.Visas)),
                new StageRow("In review", 2, g.Key, g.Sum(x => x.Visas - x.Pending / 3)),
                new StageRow("Decided", 3, g.Key, decided),
                new StageRow("Closed", 4, g.Key, Math.Min(decided, g.Sum(x => x.Closed))),
            };
        }));

    public Task<IReadOnlyList<TurnaroundRow>> GetTurnaroundAsync(ReportFilter f, string journey, CancellationToken ct) =>
        Done(Where(f).Where(x => x.Journey == journey).GroupBy(x => x.VisaType)
            .Select(g => new TurnaroundRow(g.Key, g.Sum(x => x.TurnaroundDaysTotal) / g.Sum(x => x.Visas), g.Sum(x => x.Visas))));

    public Task<IReadOnlyList<RevenueRow>> GetRevenueAsync(ReportFilter f, CancellationToken ct) =>
        Done(Where(f).GroupBy(x => (x.Country, x.Centre, x.VisaType)).Select(g => new RevenueRow(g.Key.Country, g.Key.Centre, g.Key.VisaType, g.Sum(x => x.Revenue))));

    public Task<IReadOnlyList<VisaTypeAggRow>> GetVisaTypesAsync(ReportFilter f, CancellationToken ct) =>
        Done(Where(f).GroupBy(x => x.VisaType).Select(g => new VisaTypeAggRow(g.Key, g.Sum(x => x.Visas),
            g.Sum(x => x.Approved), g.Sum(x => x.Rejected), g.Sum(x => x.Pending), g.Sum(x => x.Closed))));

    public Task<IReadOnlyList<CentreRow>> GetCentresAsync(ReportFilter f, CancellationToken ct) =>
        Done(Where(f).GroupBy(x => (x.Country, x.Centre)).Select(g =>
        {
            var months = g.Select(x => x.Month).Distinct().Count();
            var weight = Centres.First(c => c.Centre == g.Key.Centre).Weight;
            return new CentreRow(g.Key.Country, g.Key.Centre, g.Sum(x => x.Visas),
                g.Where(x => x.Journey == "centre").Sum(x => x.Visas), g.Sum(x => x.Visas - x.Pending), g.Sum(x => x.Approved),
                g.Sum(x => x.TurnaroundDaysTotal), g.Sum(x => x.Revenue), (long)Math.Round(MonthlyCapacity * weight) * months);
        }));

    public Task<FilterOptions> GetFilterOptionsAsync(CancellationToken ct) =>
        Task.FromResult(new FilterOptions("2026-01", "2026-09",
            Centres.GroupBy(c => c.Country).OrderBy(g => g.Key)
                .Select(g => new CountryOption(g.Key, g.Select(c => c.Centre).OrderBy(x => x).ToList())).ToList(),
            Types.Select(t => t.Type).OrderBy(x => x).ToList()));

    public Task<DateTimeOffset?> GetLastRefreshedUtcAsync(CancellationToken ct) =>
        Task.FromResult<DateTimeOffset?>(new DateTimeOffset(DateTime.UtcNow.Date.AddHours(DateTime.UtcNow.Hour >= 6 ? 6 : -18), TimeSpan.Zero));

    public Task<bool> PingAsync(CancellationToken ct) => Task.FromResult(true);
}
