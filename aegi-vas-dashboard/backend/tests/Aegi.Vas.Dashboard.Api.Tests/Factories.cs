using System.Security.Cryptography;
using Aegi.Vas.Dashboard.Api.Reporting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Aegi.Vas.Dashboard.Api.Tests;

/// <summary>Local developer setup: sample data, sign-in bypass.</summary>
public sealed class DevelopmentFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Development");
}

/// <summary>Hosted-environment setup: real JWT validation against a test signing key, sample rows as the source.</summary>
public class EntraFactory : WebApplicationFactory<Program>
{
    public const string Tenant = "11111111-1111-1111-1111-111111111111";
    public const string Audience = "api://22222222-2222-2222-2222-222222222222";
    public static readonly string Issuer = $"https://login.microsoftonline.com/{Tenant}/v2.0";
    public static readonly RsaSecurityKey Key = new(RSA.Create(2048)) { KeyId = "test-key" };

    public static string? LastFailure;

    protected virtual IReportingDataSource Source => new SampleDataSource();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("UAT");
        builder.UseSetting("Authentication:Mode", "Entra");
        builder.UseSetting("Authentication:TenantId", Tenant);
        builder.UseSetting("Authentication:ApiAudience", Audience);
        builder.UseSetting("Authentication:SpaClientId", "33333333-3333-3333-3333-333333333333");
        builder.UseSetting("Reporting:Source", "Fabric");
        builder.UseSetting("KeyVault:Uri", "");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IReportingDataSource>();
            services.AddSingleton(Source);
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o =>
            {
                var cfg = new OpenIdConnectConfiguration { Issuer = Issuer };
                cfg.SigningKeys.Add(Key);
                o.Configuration = cfg;
                o.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(cfg);
                o.Events = new JwtBearerEvents { OnAuthenticationFailed = c => { LastFailure = c.Exception.Message; return Task.CompletedTask; } };
            });
        });
    }

    public static string Token(string? scope = "Dashboard.Read", string? role = "Dashboard.Viewer",
        string audience = Audience, string? issuer = null, TimeSpan? lifetime = null, SecurityKey? key = null)
    {
        var now = DateTime.UtcNow;
        var claims = new Dictionary<string, object> { ["oid"] = Guid.NewGuid().ToString(), ["name"] = "Test Executive" };
        if (scope is not null) claims["scp"] = scope;
        if (role is not null) claims["roles"] = new[] { role };
        var life = lifetime ?? TimeSpan.FromMinutes(30);
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer ?? Issuer,
            Audience = audience,
            Claims = claims,
            IssuedAt = now.Add(life < TimeSpan.Zero ? life - TimeSpan.FromMinutes(10) : TimeSpan.Zero),
            NotBefore = now.Add(life < TimeSpan.Zero ? life - TimeSpan.FromMinutes(10) : TimeSpan.Zero),
            Expires = now.Add(life),
            SigningCredentials = new SigningCredentials(key ?? Key, SecurityAlgorithms.RsaSha256),
        });
    }
}

/// <summary>Reporting source that is down, to prove outages become a clean 503.</summary>
public sealed class OutageFactory : EntraFactory
{
    protected override IReportingDataSource Source => new DownSource();

    private sealed class DownSource : IReportingDataSource
    {
        public string Name => "Microsoft Fabric";
        private static Task<T> Fail<T>() => Task.FromException<T>(new ReportingUnavailableException("Reporting data is temporarily unavailable."));
        public Task<IReadOnlyList<VolumeRow>> GetVolumeAsync(ReportFilter f, CancellationToken ct) => Fail<IReadOnlyList<VolumeRow>>();
        public Task<IReadOnlyList<OutcomeRow>> GetOutcomesAsync(ReportFilter f, CancellationToken ct) => Fail<IReadOnlyList<OutcomeRow>>();
        public Task<IReadOnlyList<ClosureRow>> GetClosureAsync(ReportFilter f, CancellationToken ct) => Fail<IReadOnlyList<ClosureRow>>();
        public Task<IReadOnlyList<TurnaroundRow>> GetTurnaroundAsync(ReportFilter f, string j, CancellationToken ct) => Fail<IReadOnlyList<TurnaroundRow>>();
        public Task<IReadOnlyList<RevenueRow>> GetRevenueAsync(ReportFilter f, CancellationToken ct) => Fail<IReadOnlyList<RevenueRow>>();
        public Task<IReadOnlyList<VisaTypeAggRow>> GetVisaTypesAsync(ReportFilter f, CancellationToken ct) => Fail<IReadOnlyList<VisaTypeAggRow>>();
        public Task<Contracts.FilterOptions> GetFilterOptionsAsync(CancellationToken ct) => Fail<Contracts.FilterOptions>();
        public Task<DateTimeOffset?> GetLastRefreshedUtcAsync(CancellationToken ct) => Fail<DateTimeOffset?>();
        public Task<bool> PingAsync(CancellationToken ct) => Fail<bool>();
    }
}
