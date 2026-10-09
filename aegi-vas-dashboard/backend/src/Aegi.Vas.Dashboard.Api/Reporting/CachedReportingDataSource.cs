using Aegi.Vas.Dashboard.Api.Contracts;
using Microsoft.Extensions.Caching.Memory;

namespace Aegi.Vas.Dashboard.Api.Reporting;

/// <summary>
/// Reuses query results for identical filters. The gold layer only changes on its scheduled refresh,
/// so a short lifetime is safe. KPI results are cached only when Reporting:CacheSeconds is above zero;
/// the refresh timestamp is always held for a minute because every response carries it.
/// Not permission-scoped: every authorised executive shares the same reporting scope.
/// </summary>
public sealed class CachedReportingDataSource(IReportingDataSource inner, IMemoryCache cache, int cacheSeconds) : IReportingDataSource
{
    private static readonly TimeSpan FreshnessLifetime = TimeSpan.FromSeconds(60);
    private readonly TimeSpan _lifetime = TimeSpan.FromSeconds(Math.Max(0, cacheSeconds));

    public string Name => inner.Name;

    public Task<IReadOnlyList<VolumeRow>> GetVolumeAsync(ReportFilter f, CancellationToken ct) => Get($"volume|{f}", () => inner.GetVolumeAsync(f, ct));
    public Task<IReadOnlyList<OutcomeRow>> GetOutcomesAsync(ReportFilter f, CancellationToken ct) => Get($"outcomes|{f}", () => inner.GetOutcomesAsync(f, ct));
    public Task<IReadOnlyList<ClosureRow>> GetClosureAsync(ReportFilter f, CancellationToken ct) => Get($"closure|{f}", () => inner.GetClosureAsync(f, ct));
    public Task<IReadOnlyList<StageRow>> GetStagesAsync(ReportFilter f, CancellationToken ct) => Get($"stages|{f}", () => inner.GetStagesAsync(f, ct));
    public Task<IReadOnlyList<TurnaroundRow>> GetTurnaroundAsync(ReportFilter f, string journey, CancellationToken ct) => Get($"turnaround|{journey}|{f}", () => inner.GetTurnaroundAsync(f, journey, ct));
    public Task<IReadOnlyList<RevenueRow>> GetRevenueAsync(ReportFilter f, CancellationToken ct) => Get($"revenue|{f}", () => inner.GetRevenueAsync(f, ct));
    public Task<IReadOnlyList<VisaTypeAggRow>> GetVisaTypesAsync(ReportFilter f, CancellationToken ct) => Get($"visa-types|{f}", () => inner.GetVisaTypesAsync(f, ct));
    public Task<IReadOnlyList<CentreRow>> GetCentresAsync(ReportFilter f, CancellationToken ct) => Get($"centres|{f}", () => inner.GetCentresAsync(f, ct));

    public Task<FilterOptions> GetFilterOptionsAsync(CancellationToken ct) => inner.GetFilterOptionsAsync(ct);

    public Task<DateTimeOffset?> GetLastRefreshedUtcAsync(CancellationToken ct) =>
        cache.GetOrCreateAsync("reporting|last-refreshed", e =>
        {
            e.AbsoluteExpirationRelativeToNow = FreshnessLifetime;
            return inner.GetLastRefreshedUtcAsync(ct);
        });

    public Task<bool> PingAsync(CancellationToken ct) => inner.PingAsync(ct);

    private Task<T> Get<T>(string key, Func<Task<T>> load) =>
        _lifetime == TimeSpan.Zero ? load() : cache.GetOrCreateAsync($"reporting|{key}", e =>
        {
            e.AbsoluteExpirationRelativeToNow = _lifetime;
            return load();
        })!;
}
