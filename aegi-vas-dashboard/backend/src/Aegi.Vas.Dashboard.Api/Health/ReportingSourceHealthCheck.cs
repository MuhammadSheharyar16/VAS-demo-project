using Aegi.Vas.Dashboard.Api.Reporting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Aegi.Vas.Dashboard.Api.Health;

/// <summary>
/// Readiness: can the backend reach the reporting source with its own credential?
/// The endpoint is anonymous, so the result is held for 30 seconds and callers cannot drive queries against Fabric.
/// </summary>
public sealed class ReportingSourceHealthCheck(IReportingDataSource source, IMemoryCache cache) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default) =>
        cache.GetOrCreateAsync("health|reporting-source", e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
            return ProbeAsync(ct);
        });

    private async Task<HealthCheckResult> ProbeAsync(CancellationToken ct)
    {
        try
        {
            return await source.PingAsync(ct)
                ? HealthCheckResult.Healthy($"{source.Name} reachable")
                : HealthCheckResult.Unhealthy($"{source.Name} returned no result");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"{source.Name} unreachable", ex);
        }
    }
}
