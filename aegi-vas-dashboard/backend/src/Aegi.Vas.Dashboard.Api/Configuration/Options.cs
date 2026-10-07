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
    /// <summary>Application ID URI of the API registration, e.g. api://{client-id}.</summary>
    public string ApiAudience { get; set; } = "";
    /// <summary>Delegated permission exposed by the API that the SPA requests.</summary>
    public string RequiredScope { get; set; } = "Dashboard.Read";
    /// <summary>App role assigned to the executives allowed to use the dashboard.</summary>
    public string RequiredRole { get; set; } = "Dashboard.Viewer";
    /// <summary>Public client ID of the SPA registration (not a secret).</summary>
    public string SpaClientId { get; set; } = "";
}

public enum ReportingSource { Fabric, Sample }

public sealed class ReportingSettings
{
    public const string Section = "Reporting";
    public ReportingSource Source { get; set; } = ReportingSource.Fabric;
    public string Currency { get; set; } = "USD";
    public string TimeZone { get; set; } = "UTC";
    /// <summary>Hours after the last gold refresh when data is flagged stale. Null = not agreed yet, never flagged.</summary>
    public double? StaleAfterHours { get; set; }
    public string DefaultFrom { get; set; } = "2026-01";
    public string DefaultTo { get; set; } = "2026-09";
}

public sealed class FabricSettings
{
    public const string Section = "Fabric";
    /// <summary>SQL analytics endpoint host of the Lakehouse.</summary>
    public string SqlEndpoint { get; set; } = "";
    public string Database { get; set; } = "";
    /// <summary>Client ID of the backend's Fabric service principal.</summary>
    public string ClientId { get; set; } = "";
    /// <summary>Loaded from Key Vault secret "Fabric--ClientSecret" in hosted environments.</summary>
    public string ClientSecret { get; set; } = "";
    public int CommandTimeoutSeconds { get; set; } = 30;
}
