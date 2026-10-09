namespace Aegi.Vas.Dashboard.Api.Configuration;

public enum AuthenticationMode
{
    /// <summary>Executive tokens from Microsoft Entra ID are required (all hosted environments).</summary>
    Entra,
    /// <summary>Local development only: every caller is a signed-in viewer. Refused outside Development.</summary>
    DevelopmentBypass,
}

public sealed class AuthenticationSettings
{
    public const string Section = "Authentication";
    public AuthenticationMode Mode { get; set; } = AuthenticationMode.Entra;
    public string TenantId { get; set; } = "";
    /// <summary>Application ID URI of the API registration, e.g. api://{client-id}. The SPA requests {ApiAudience}/{RequiredScope}.</summary>
    public string ApiAudience { get; set; } = "";
    /// <summary>
    /// Client ID (GUID) of the API registration. v2 access tokens carry this as their audience.
    /// Required when ApiAudience is a custom URI rather than api://{client-id}.
    /// </summary>
    public string ApiClientId { get; set; } = "";
    /// <summary>Delegated permission exposed by the API that the SPA requests.</summary>
    public string RequiredScope { get; set; } = "Dashboard.Read";
    /// <summary>App role assigned to the executives allowed to use the dashboard.</summary>
    public string RequiredRole { get; set; } = "Dashboard.Viewer";
    /// <summary>Public client ID of the SPA registration (not a secret).</summary>
    public string SpaClientId { get; set; } = "";
}

public enum ReportingSource { Fabric, Sample }

/// <summary>
/// Status bands for one KPI. For a KPI where higher is better, a value below Watch is "watch" and below Poor is "poor";
/// for a KPI where lower is better the comparison is reversed.
/// </summary>
public sealed class KpiThreshold
{
    public decimal Watch { get; set; }
    public decimal Poor { get; set; }
}

public sealed class ReportingSettings
{
    public const string Section = "Reporting";
    public ReportingSource Source { get; set; } = ReportingSource.Fabric;
    public string Currency { get; set; } = "USD";
    public string TimeZone { get; set; } = "UTC";
    /// <summary>Hours after the last gold refresh when data is flagged stale. Null = not agreed yet, never flagged.</summary>
    public double? StaleAfterHours { get; set; }
    /// <summary>Seconds to reuse a query result for the same filters. 0 = off (default until the UAT load test says otherwise).</summary>
    public int CacheSeconds { get; set; }
    /// <summary>Status thresholds by KPI key (e.g. approval_rate). A KPI without an agreed threshold shows no status.</summary>
    public Dictionary<string, KpiThreshold> Thresholds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public enum FabricCredential
{
    /// <summary>The App Service managed identity (all hosted environments). No secret is stored anywhere.</summary>
    ManagedIdentity,
    /// <summary>Developer sign-in (az login, Visual Studio) for running against Fabric from a workstation.</summary>
    Default,
}

public sealed class FabricSettings
{
    public const string Section = "Fabric";
    /// <summary>SQL analytics endpoint host of the Lakehouse.</summary>
    public string SqlEndpoint { get; set; } = "";
    public string Database { get; set; } = "";
    public FabricCredential Credential { get; set; } = FabricCredential.ManagedIdentity;
    /// <summary>Client ID of a user-assigned managed identity. Empty = the system-assigned identity.</summary>
    public string ManagedIdentityClientId { get; set; } = "";
    /// <summary>TLS mode for the SQL connection: Mandatory (default) or Strict (TDS 8.0, only if the endpoint accepts it).</summary>
    public string Encrypt { get; set; } = "Mandatory";
    public int CommandTimeoutSeconds { get; set; } = 30;
}
