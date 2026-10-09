namespace Aegi.Vas.Dashboard.Api.Contracts;

// Every endpoint returns { data, meta }. Meta carries what the UI needs to label numbers honestly:
// the period and filters applied, currency, and when the gold layer was last refreshed.

public sealed record ApiResponse<T>(T Data, ResponseMeta Meta);

public sealed record ResponseMeta(
    AppliedFilters Filters,
    string Currency,
    string TimeZone,
    DataFreshness Freshness,
    DateTimeOffset GeneratedUtc,
    string CorrelationId);

public sealed record AppliedFilters(string From, string To, string? Country, string? Centre, string? VisaType);

public sealed record DataFreshness(DateTimeOffset? LastRefreshedUtc, bool IsStale, string Source);

// ---- Executive Summary ----

/// <summary>
/// One headline KPI. Previous is the same KPI for the comparison period; Change is the movement since then,
/// in ChangeUnit: "percent" (relative, for counts and money), "points" (for rates) or "days".
/// Status is "good", "watch" or "poor" when a threshold is agreed for the KPI, otherwise null.
/// </summary>
public sealed record Kpi(
    string Key, string Label, decimal? Value, string Unit,
    decimal? Previous, decimal? Change, string? ChangeUnit, bool? HigherIsBetter, string? Status);

public sealed record Highlight(string Kind, string Title, string Detail);

public sealed record PeriodRange(string From, string To);

public sealed record SummaryData(
    IReadOnlyList<Kpi> Kpis,
    PeriodRange? ComparedWith,
    VolumeData Volume,
    OutcomesData Outcomes,
    ClosureData Closure,
    IReadOnlyList<Highlight> Highlights,
    IReadOnlyList<string> ComingSoon);

// ---- VAS ----
public sealed record VolumePoint(string Month, long Centre, long Digital, long Total);
public sealed record VolumeData(long Total, IReadOnlyList<VolumePoint> Months);

public sealed record OutcomeShare(string Outcome, long Visas, decimal Percent);
public sealed record OutcomesData(
    long Received, long Processed, long Pending,
    decimal? ApprovalPercent, decimal? RejectionPercent,
    IReadOnlyList<OutcomeShare> Outcomes);

public sealed record ClosurePoint(string Month, decimal ClosurePercent);
public sealed record ClosureData(decimal? ClosurePercent, IReadOnlyList<ClosurePoint> Months);

public sealed record StageItem(string Stage, long Centre, long Digital, long Total, decimal PercentOfFirst);
public sealed record StagesData(IReadOnlyList<StageItem> Stages);

public sealed record TurnaroundItem(string VisaType, decimal AvgDays);
public sealed record TurnaroundData(string Journey, decimal? AverageDays, IReadOnlyList<TurnaroundItem> ByVisaType);

public sealed record CentreRevenue(string Centre, decimal Revenue);
public sealed record CountryRevenue(string Country, decimal Revenue, decimal SharePercent, IReadOnlyList<CentreRevenue> Centres);
public sealed record VisaTypeRevenue(string VisaType, decimal Revenue, decimal SharePercent);
public sealed record RevenueData(decimal Total, IReadOnlyList<CountryRevenue> Countries, IReadOnlyList<VisaTypeRevenue> ByVisaType);

public sealed record VisaTypeRow(
    string VisaType, long Volume, decimal SharePercent,
    decimal ApprovedPercent, decimal RejectedPercent, decimal PendingPercent, decimal ClosurePercent);
public sealed record VisaTypesData(IReadOnlyList<VisaTypeRow> VisaTypes);

public sealed record CentrePerformance(
    string Country, string Centre, long Applications, long CentreJourney, long Digital, long Processed,
    decimal? ApprovalPercent, decimal? AvgTurnaroundDays, decimal Revenue, decimal? UtilisationPercent);
public sealed record CentresData(IReadOnlyList<CentrePerformance> Centres);

// ---- Filter options ----
public sealed record CountryOption(string Country, IReadOnlyList<string> Centres);
public sealed record FilterOptions(string MinMonth, string MaxMonth, IReadOnlyList<CountryOption> Countries, IReadOnlyList<string> VisaTypes);

// ---- SPA bootstrap (public, no secrets) ----
public sealed record ClientConfig(bool AuthEnabled, string? TenantId, string? ClientId, string? ApiScope, string DataSource);
