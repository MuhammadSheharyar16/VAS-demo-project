using System.Security.Claims;
using System.Text.Encodings.Web;
using Aegi.Vas.Dashboard.Api.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Aegi.Vas.Dashboard.Api.Security;

public static class AuthenticationSetup
{
    public const string DashboardAccessPolicy = "DashboardAccess";
    private const string BypassScheme = "DevelopmentBypass";

    /// <summary>
    /// Entra mode validates the executive's API access token: signature (tenant signing keys), issuer,
    /// audience (this API) and lifetime. The DashboardAccess policy then requires the delegated scope
    /// and the assigned app role. The executive token is only ever used to call this API.
    /// </summary>
    public static IServiceCollection AddDashboardAuthentication(this IServiceCollection services, IConfiguration config, IHostEnvironment env)
    {
        var auth = config.GetSection(AuthenticationSettings.Section).Get<AuthenticationSettings>() ?? new();

        if (auth.Mode == AuthenticationMode.DevelopmentBypass)
        {
            if (!env.IsDevelopment())
                throw new InvalidOperationException("Authentication:Mode=DevelopmentBypass is only allowed in the Development environment.");
            services.AddAuthentication(BypassScheme)
                .AddScheme<AuthenticationSchemeOptions, DevelopmentBypassHandler>(BypassScheme, _ => { });
        }
        else
        {
            if (string.IsNullOrWhiteSpace(auth.TenantId) || string.IsNullOrWhiteSpace(auth.ApiAudience))
                throw new InvalidOperationException("Authentication:TenantId and Authentication:ApiAudience are required.");

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
            {
                o.Authority = $"https://login.microsoftonline.com/{auth.TenantId}/v2.0";
                o.MapInboundClaims = false; // keep Entra claim names: scp, roles, oid
                o.TokenValidationParameters.ValidAudiences = [auth.ApiAudience, auth.ApiAudience.Replace("api://", "", StringComparison.Ordinal)];
                o.TokenValidationParameters.ValidIssuer = $"https://login.microsoftonline.com/{auth.TenantId}/v2.0";
                o.TokenValidationParameters.ValidateIssuer = true;
                o.TokenValidationParameters.ValidateAudience = true;
                o.TokenValidationParameters.ValidateLifetime = true;
                o.TokenValidationParameters.ValidateIssuerSigningKey = true;
                o.TokenValidationParameters.RequireExpirationTime = true;
                o.TokenValidationParameters.RequireSignedTokens = true;
                o.TokenValidationParameters.ClockSkew = TimeSpan.FromMinutes(2);
                o.TokenValidationParameters.NameClaimType = "name";
                o.TokenValidationParameters.RoleClaimType = "roles";
            });
        }

        services.AddAuthorizationBuilder().AddPolicy(DashboardAccessPolicy, p => p
            .RequireAuthenticatedUser()
            .RequireAssertion(ctx => HasScope(ctx.User, auth.RequiredScope))
            .RequireClaim("roles", auth.RequiredRole));
        return services;
    }

    private static bool HasScope(ClaimsPrincipal user, string scope) =>
        user.FindAll("scp").SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(scope, StringComparer.Ordinal);

    /// <summary>Local development only: signs every request in as a viewer with the required scope and role.</summary>
    private sealed class DevelopmentBypassHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
        IOptions<AuthenticationSettings> auth)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(
            [
                new Claim("oid", "00000000-0000-0000-0000-000000000000"),
                new Claim("name", "Local developer"),
                new Claim("scp", auth.Value.RequiredScope),
                new Claim("roles", auth.Value.RequiredRole),
            ], BypassScheme, "name", "roles");
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), BypassScheme)));
        }
    }
}
