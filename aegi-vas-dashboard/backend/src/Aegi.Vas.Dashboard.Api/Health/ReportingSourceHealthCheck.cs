using Aegi.Vas.Dashboard.Api.Reporting;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Aegi.Vas.Dashboard.Api.Health;

/// <summary>Readiness: can the backend reach the reporting source with its own credential?</summary>
public sealed class ReportingSourceHealthCheck(IReportingDataSource source) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
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
