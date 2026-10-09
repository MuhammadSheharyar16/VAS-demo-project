using Aegi.Vas.Dashboard.Api.Configuration;
using Aegi.Vas.Dashboard.Api.Contracts;
using Aegi.Vas.Dashboard.Api.Reporting;
using Microsoft.Extensions.Options;

namespace Aegi.Vas.Dashboard.Api.Services;

/// <summary>
/// Shapes source rows into dashboard payloads. All percentages, averages, comparisons and statuses the UI shows
/// are produced here, so the React app never carries its own copy of a KPI formula.
/// Formula choices marked ASSUMPTION must be confirmed in the KPI Mapping document (requirements section 3.3).
/// </summary>
public sealed class KpiService(IReportingDataSource source, IOptions<ReportingSettings> reporting, TimeProvider clock)
{
    private readonly ReportingSettings _settings = reporting.Value;

    // Requirements section 2: only these three areas are Coming Soon placeholders. CSAT is out of scope, not promised.
    public static readonly string[] ComingSoon = ["Operations", "Finance", "Alfit"];

    public async Task<ResponseMeta> MetaAsync(ReportFilter f, string correlationId, CancellationToken ct)
    {
        var refreshed = await source.GetLastRefreshedUtcAsync(ct);
        var now = clock.GetUtcNow();
        var stale = refreshed is { } r && _settings.StaleAfterHours is { } h && (now - r).TotalHours > h;
        return new ResponseMeta(new AppliedFilters(f.From, f.To, f.Country, f.Centre, f.VisaType), _settings.Currency, _settings.TimeZone,
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

    /// <summary>
    /// ASSUMPTION: received = every application in the period; pending = those with outcome "Pending";
    /// processed = received minus pending. Approval and rejection rates are shares of all received applications.
    /// </summary>
    public async Task<OutcomesData> OutcomesAsync(ReportFilter f, CancellationToken ct)
    {
        var rows = await source.GetOutcomesAsync(f, ct);
        var received = rows.Sum(r => r.Visas);
        long Of(string outcome) => rows.Where(r => r.Outcome.Equals(outcome, StringComparison.OrdinalIgnoreCase)).Sum(r => r.Visas);
        var pending = Of("Pending");
        return new OutcomesData(received, received - pending, pending, Pct(Of("Approved"), received), Pct(Of("Rejected"), received),
            rows.OrderByDescending(r => r.Visas).Select(r => new OutcomeShare(r.Outcome, r.Visas, Pct(r.Visas, received) ?? 0)).ToList());
    }

    /// <summary>ASSUMPTION: closure % = closed applications / all applications in the period.</summary>
    public async Task<ClosureData> ClosureAsync(ReportFilter f, CancellationToken ct)
    {
        var rows = await source.GetClosureAsync(f, ct);
        return new ClosureData(Pct(rows.Sum(r => r.Closed), rows.Sum(r => r.Total)),
            rows.OrderBy(r => r.Month).Select(r => new ClosurePoint(r.Month, Pct(r.Closed, r.Total) ?? 0)).ToList());
    }

    /// <summary>Stage names and their order come from the data; each stage is shown against the first one.</summary>
    public async Task<StagesData> StagesAsync(ReportFilter f, CancellationToken ct)
    {
        var rows = await source.GetStagesAsync(f, ct);
        var stages = rows.GroupBy(r => (r.Order, r.Stage)).OrderBy(g => g.Key.Order).Select(g => (
            g.Key.Stage,
            Centre: g.Where(r => r.Journey == "centre").Sum(r => r.Applications),
            Digital: g.Where(r => r.Journey == "digital").Sum(r => r.Applications),
            Total: g.Sum(r => r.Applications))).ToList();
        var first = stages.Count == 0 ? 0 : stages[0].Total;
        return new StagesData(stages.Select(s => new StageItem(s.Stage, s.Centre, s.Digital, s.Total, Pct(s.Total, first) ?? 0)).ToList());
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
                g.GroupBy(r => r.Centre).Select(c => new CentreRevenue(c.Key, Math.Round(c.Sum(r => r.Revenue), 2)))
                    .OrderByDescending(c => c.Revenue).ToList());
        }).OrderByDescending(c => c.Revenue).ToList();
        var byVisaType = rows.GroupBy(r => r.VisaType).Select(g =>
        {
            var sum = g.Sum(r => r.Revenue);
            return new VisaTypeRevenue(g.Key, Math.Round(sum, 2), Pct(sum, total) ?? 0);
        }).OrderByDescending(v => v.Revenue).ToList();
        return new RevenueData(Math.Round(total, 2), countries, byVisaType);
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

    /// <summary>
    /// ASSUMPTION: utilisation = applications handled in the centre (centre journey) / the centre's capacity for the
    /// same months. Capacity is not recorded per visa type, so utilisation is left empty when a visa type is selected.
    /// </summary>
    public async Task<CentresData> CentresAsync(ReportFilter f, CancellationToken ct)
    {
        var rows = await source.GetCentresAsync(f, ct);
        return new CentresData(rows.OrderByDescending(r => r.Applications).Select(r => new CentrePerformance(
            r.Country, r.Centre, r.Applications, r.CentreJourney, r.Applications - r.CentreJourney, r.Processed,
            Pct(r.Approved, r.Applications),
            r.Applications == 0 ? null : Math.Round(r.TurnaroundDays / r.Applications, 1),
            Math.Round(r.Revenue, 2),
            f.VisaType is null && r.Capacity is { } capacity ? Pct(r.CentreJourney, capacity) : null)).ToList());
    }

    public async Task<SummaryData> SummaryAsync(ReportFilter f, CancellationToken ct)
    {
        // The comparison period runs alongside the selected one: two connections per request, each used sequentially.
        var previousFilter = f.PreviousPeriod();
        var previousTask = SnapshotAsync(previousFilter, ct);
        var volume = await VolumeAsync(f, ct);
        var current = await SnapshotAsync(f, ct);
        var previous = await previousTask;
        var compared = previous.Outcomes.Received > 0;
        Snapshot? p = compared ? previous : null;

        var kpis = new List<Kpi>
        {
            Make("applications_received", "Applications received", current.Outcomes.Received, "count", p?.Outcomes.Received, true),
            Make("applications_processed", "Applications processed", current.Outcomes.Processed, "count", p?.Outcomes.Processed, true),
            Make("applications_pending", "Applications pending", current.Outcomes.Pending, "count", p?.Outcomes.Pending, false),
            Make("approval_rate", "Approval rate", current.Outcomes.ApprovalPercent, "percent", p?.Outcomes.ApprovalPercent, true),
            Make("rejection_rate", "Rejection rate", current.Outcomes.RejectionPercent, "percent", p?.Outcomes.RejectionPercent, false),
            Make("closure_rate", "Visa closure", current.Closure.ClosurePercent, "percent", p?.Closure.ClosurePercent, true),
            Make("turnaround_digital", "Turnaround · digital", current.Digital.AverageDays, "days", p?.Digital.AverageDays, false),
            Make("turnaround_centre", "Turnaround · centre", current.Centre.AverageDays, "days", p?.Centre.AverageDays, false),
            Make("revenue", "VAS revenue", current.Revenue.Total, "currency", p?.Revenue.Total, true),
        };
        return new SummaryData(kpis, compared ? new PeriodRange(previousFilter.From, previousFilter.To) : null,
            volume, current.Outcomes, current.Closure, Highlights(volume, current.Centre, current.Revenue), ComingSoon);
    }

    private sealed record Snapshot(OutcomesData Outcomes, ClosureData Closure, TurnaroundData Digital, TurnaroundData Centre, RevenueData Revenue);

    private async Task<Snapshot> SnapshotAsync(ReportFilter f, CancellationToken ct) => new(
        await OutcomesAsync(f, ct), await ClosureAsync(f, ct),
        await TurnaroundAsync(f, "digital", ct), await TurnaroundAsync(f, "centre", ct), await RevenueAsync(f, ct));

    /// <summary>Counts and money move by a relative percentage; rates move in percentage points; turnaround moves in days.</summary>
    private Kpi Make(string key, string label, decimal? value, string unit, decimal? previous, bool higherIsBetter)
    {
        decimal? change = null;
        var changeUnit = unit switch { "percent" => "points", "days" => "days", _ => "percent" };
        if (value is { } v && previous is { } p)
            change = changeUnit == "percent" ? Pct(v - p, p) : Math.Round(v - p, 1);

        string? status = null;
        if (value is { } current && _settings.Thresholds.TryGetValue(key, out var t))
            status = higherIsBetter
                ? current < t.Poor ? "poor" : current < t.Watch ? "watch" : "good"
                : current > t.Poor ? "poor" : current > t.Watch ? "watch" : "good";
        return new Kpi(key, label, value, unit, previous, change, change is null ? null : changeUnit, higherIsBetter, status);
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
        if (slowest is not null && centre.ByVisaType.Count > 1 && centre.AverageDays is { } avg)
            list.Add(new("attention", $"{slowest.VisaType} visas take the longest",
                $"Centre turnaround for {slowest.VisaType.ToLowerInvariant()} visas averages {slowest.AvgDays:0.0} days, against {avg:0.0} across all types."));
        var top = revenue.Countries.FirstOrDefault();
        if (top is not null && revenue.Countries.Count > 1)
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
