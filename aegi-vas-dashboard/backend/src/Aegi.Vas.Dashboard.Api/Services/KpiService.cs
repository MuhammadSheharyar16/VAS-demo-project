using Aegi.Vas.Dashboard.Api.Configuration;
using Aegi.Vas.Dashboard.Api.Contracts;
using Aegi.Vas.Dashboard.Api.Reporting;
using Microsoft.Extensions.Options;

namespace Aegi.Vas.Dashboard.Api.Services;

/// <summary>
/// Shapes source rows into dashboard payloads. All percentages and averages the UI shows are produced here,
/// so the React app never carries its own copy of a KPI formula.
/// Formula choices marked ASSUMPTION must be confirmed in the reporting contract (roadmap phase 2).
/// </summary>
public sealed class KpiService(IReportingDataSource source, IOptions<ReportingSettings> reporting, TimeProvider clock)
{
    private readonly ReportingSettings _settings = reporting.Value;

    public static readonly string[] ComingSoon = ["CSAT", "Operations", "Finance", "Alfit"];

    public async Task<ResponseMeta> MetaAsync(ReportFilter f, string correlationId, CancellationToken ct)
    {
        var refreshed = await source.GetLastRefreshedUtcAsync(ct);
        var now = clock.GetUtcNow();
        var stale = refreshed is { } r && _settings.StaleAfterHours is { } h && (now - r).TotalHours > h;
        return new ResponseMeta(new AppliedFilters(f.From, f.To, f.Country, f.Centre), _settings.Currency, _settings.TimeZone,
            new DataFreshness(refreshed, stale, source.Name), now, correlationId);
    }

    public async Task<VolumeData> VolumeAsync(ReportFilter f, CancellationToken ct)
    {
        var rows = await source.GetVolumeAsync(f, ct);
        var months = rows.GroupBy(r => r.Month).OrderBy(g => g.Key).Select(g =>
        {
            var centre = g.Where(r => r.Journey == "centre").Sum(r => r.Visas);
            var digital = g.Where(r => r.Journey == "digital").Sum(r => r.Visas);
            return new VolumePoint(g.Key, centre, digital, centre + digital);
        }).ToList();
        return new VolumeData(months.Sum(m => m.Total), months);
    }

    public async Task<OutcomesData> OutcomesAsync(ReportFilter f, CancellationToken ct)
    {
        var rows = await source.GetOutcomesAsync(f, ct);
        var total = rows.Sum(r => r.Visas);
        return new OutcomesData(total, rows.OrderByDescending(r => r.Visas)
            .Select(r => new OutcomeShare(r.Outcome, r.Visas, Pct(r.Visas, total) ?? 0)).ToList());
    }

    /// <summary>ASSUMPTION: closure % = closed applications / all applications in the period.</summary>
    public async Task<ClosureData> ClosureAsync(ReportFilter f, CancellationToken ct)
    {
        var rows = await source.GetClosureAsync(f, ct);
        return new ClosureData(Pct(rows.Sum(r => r.Closed), rows.Sum(r => r.Total)),
            rows.OrderBy(r => r.Month).Select(r => new ClosurePoint(r.Month, Pct(r.Closed, r.Total) ?? 0)).ToList());
    }

    /// <summary>ASSUMPTION: turnaround = days from application to decision, averaged per application.</summary>
    public async Task<TurnaroundData> TurnaroundAsync(ReportFilter f, string journey, CancellationToken ct)
    {
        var rows = await source.GetTurnaroundAsync(f, journey, ct);
        var apps = rows.Sum(r => r.Applications);
        decimal? avg = apps == 0 ? null : Math.Round(rows.Sum(r => r.AvgDays * r.Applications) / apps, 1);
        return new TurnaroundData(journey, avg, rows.OrderBy(r => r.VisaType, VisaTypeOrder.Instance)
            .Select(r => new TurnaroundItem(r.VisaType, Math.Round(r.AvgDays, 1))).ToList());
    }

    public async Task<RevenueData> RevenueAsync(ReportFilter f, CancellationToken ct)
    {
        var rows = await source.GetRevenueAsync(f, ct);
        var total = rows.Sum(r => r.Revenue);
        var countries = rows.GroupBy(r => r.Country).Select(g =>
        {
            var sum = g.Sum(r => r.Revenue);
            return new CountryRevenue(g.Key, Math.Round(sum, 2), Pct(sum, total) ?? 0,
                g.OrderByDescending(r => r.Revenue).Select(r => new CentreRevenue(r.Centre, Math.Round(r.Revenue, 2))).ToList());
        }).OrderByDescending(c => c.Revenue).ToList();
        return new RevenueData(Math.Round(total, 2), countries);
    }

    public async Task<VisaTypesData> VisaTypesAsync(ReportFilter f, CancellationToken ct)
    {
        var rows = await source.GetVisaTypesAsync(f, ct);
        var total = rows.Sum(r => r.Volume);
        return new VisaTypesData(rows.OrderByDescending(r => r.Volume).Select(r => new VisaTypeRow(
            r.VisaType, r.Volume, Pct(r.Volume, total) ?? 0,
            Pct(r.Approved, r.Volume) ?? 0, Pct(r.Rejected, r.Volume) ?? 0, Pct(r.Pending, r.Volume) ?? 0,
            Pct(r.Closed, r.Volume) ?? 0)).ToList());
    }

    public async Task<SummaryData> SummaryAsync(ReportFilter f, CancellationToken ct)
    {
        // Sequential on purpose: one pooled connection per request keeps Fabric load predictable.
        var volume = await VolumeAsync(f, ct);
        var outcomes = await OutcomesAsync(f, ct);
        var closure = await ClosureAsync(f, ct);
        var digital = await TurnaroundAsync(f, "digital", ct);
        var centre = await TurnaroundAsync(f, "centre", ct);
        var revenue = await RevenueAsync(f, ct);

        // ASSUMPTION: approval rate = approved / all recorded outcomes (including pending and withdrawn).
        var approved = outcomes.Outcomes.FirstOrDefault(o => o.Outcome.Equals("Approved", StringComparison.OrdinalIgnoreCase));
        var kpis = new List<Kpi>
        {
            new("visas_processed", "Visas processed", volume.Total, "count"),
            new("approval_rate", "Approval rate", outcomes.Total == 0 ? null : approved?.Percent ?? 0, "percent"),
            new("closure_rate", "Visa closure", closure.ClosurePercent, "percent"),
            new("turnaround_digital", "Turnaround · digital", digital.AverageDays, "days"),
            new("turnaround_centre", "Turnaround · centre", centre.AverageDays, "days"),
            new("revenue", "VAS revenue", revenue.Total, "currency"),
        };
        return new SummaryData(kpis, Highlights(volume, centre, revenue), ComingSoon);
    }

    private static List<Highlight> Highlights(VolumeData volume, TurnaroundData centre, RevenueData revenue)
    {
        var list = new List<Highlight>();
        if (volume.Months.Count > 1 && volume.Total > 0)
        {
            var first = volume.Months[0];
            var last = volume.Months[^1];
            var share = Pct(volume.Months.Sum(m => m.Digital), volume.Total);
            var growth = first.Digital == 0 ? null : Pct(last.Digital - first.Digital, first.Digital);
            if (growth is { } g)
                list.Add(new("trend", g >= 0 ? "Digital journey is growing" : "Digital journey is shrinking",
                    $"Digital applications are {(g >= 0 ? "up" : "down")} {Math.Abs(Math.Round(g)):0}% from {MonthName(first.Month)} to {MonthName(last.Month)} and make up {share:0}% of volume."));
        }
        var slowest = centre.ByVisaType.OrderByDescending(t => t.AvgDays).FirstOrDefault();
        if (slowest is not null && centre.AverageDays is { } avg)
            list.Add(new("attention", $"{slowest.VisaType} visas take the longest",
                $"Centre turnaround for {slowest.VisaType.ToLowerInvariant()} visas averages {slowest.AvgDays:0.0} days, against {avg:0.0} across all types."));
        var top = revenue.Countries.FirstOrDefault();
        if (top is not null)
            list.Add(new("info", $"{top.Country} leads revenue", $"{top.Country} contributes {top.SharePercent:0}% of VAS revenue in this period."));
        return list;
    }

    private static string MonthName(string yyyyMm) =>
        DateTime.ParseExact(yyyyMm, "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture).ToString("MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);

    private static decimal? Pct(decimal part, decimal whole) => whole == 0 ? null : Math.Round(part / whole * 100, 1);
    private static decimal? Pct(long part, long whole) => Pct((decimal)part, whole);

    private sealed class VisaTypeOrder : IComparer<string>
    {
        public static readonly VisaTypeOrder Instance = new();
        private static readonly string[] Order = ["Tourist", "Visit", "Business", "Work", "Student", "Family"];
        public int Compare(string? x, string? y)
        {
            int Rank(string? v) { var i = Array.IndexOf(Order, v); return i < 0 ? int.MaxValue : i; }
            var c = Rank(x).CompareTo(Rank(y));
            return c != 0 ? c : string.CompareOrdinal(x, y);
        }
    }
}
