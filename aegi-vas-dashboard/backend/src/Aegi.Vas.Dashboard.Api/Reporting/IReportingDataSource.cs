using Aegi.Vas.Dashboard.Api.Contracts;

namespace Aegi.Vas.Dashboard.Api.Reporting;

// Row shapes returned by the approved gold reporting views. Aggregation happens at the source;
// KpiService only turns these rows into dashboard payloads.
public sealed record VolumeRow(string Month, string Journey, long Visas);
public sealed record OutcomeRow(string Outcome, long Visas);
public sealed record ClosureRow(string Month, long Closed, long Total);
public sealed record StageRow(string Stage, int Order, string Journey, long Applications);
public sealed record TurnaroundRow(string VisaType, decimal AvgDays, long Applications);
public sealed record RevenueRow(string Country, string Centre, string VisaType, decimal Revenue);
public sealed record VisaTypeAggRow(string VisaType, long Volume, long Approved, long Rejected, long Pending, long Closed);
public sealed record CentreRow(string Country, string Centre, long Applications, long CentreJourney, long Processed, long Approved,
    decimal TurnaroundDays, decimal Revenue, long? Capacity);

public interface IReportingDataSource
{
    string Name { get; }
    Task<IReadOnlyList<VolumeRow>> GetVolumeAsync(ReportFilter f, CancellationToken ct);
    Task<IReadOnlyList<OutcomeRow>> GetOutcomesAsync(ReportFilter f, CancellationToken ct);
    Task<IReadOnlyList<ClosureRow>> GetClosureAsync(ReportFilter f, CancellationToken ct);
    Task<IReadOnlyList<StageRow>> GetStagesAsync(ReportFilter f, CancellationToken ct);
    Task<IReadOnlyList<TurnaroundRow>> GetTurnaroundAsync(ReportFilter f, string journey, CancellationToken ct);
    Task<IReadOnlyList<RevenueRow>> GetRevenueAsync(ReportFilter f, CancellationToken ct);
    Task<IReadOnlyList<VisaTypeAggRow>> GetVisaTypesAsync(ReportFilter f, CancellationToken ct);
    Task<IReadOnlyList<CentreRow>> GetCentresAsync(ReportFilter f, CancellationToken ct);
    Task<FilterOptions> GetFilterOptionsAsync(CancellationToken ct);
    Task<DateTimeOffset?> GetLastRefreshedUtcAsync(CancellationToken ct);
    Task<bool> PingAsync(CancellationToken ct);
}

/// <summary>Thrown when the reporting source cannot be reached; mapped to HTTP 503.</summary>
public sealed class ReportingUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
