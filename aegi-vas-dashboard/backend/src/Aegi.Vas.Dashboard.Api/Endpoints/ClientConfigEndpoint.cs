using Aegi.Vas.Dashboard.Api.Configuration;
using Aegi.Vas.Dashboard.Api.Contracts;
using Aegi.Vas.Dashboard.Api.Reporting;
using Microsoft.Extensions.Options;

namespace Aegi.Vas.Dashboard.Api.Endpoints;

public static class ClientConfigEndpoint
{
    /// <summary>
    /// Public sign-in settings for the SPA, so one React build works in Dev, UAT and Production.
    /// Contains only public identifiers; no secrets.
    /// </summary>
    public static void MapClientConfig(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/client-config", (IOptions<AuthenticationSettings> auth, IReportingDataSource source) =>
        {
            var a = auth.Value;
            var entra = a.Mode == AuthenticationMode.Entra;
            return Results.Ok(new ClientConfig(entra,
                entra ? a.TenantId : null,
                entra ? a.SpaClientId : null,
                entra ? $"{a.ApiAudience}/{a.RequiredScope}" : null,
                source.Name));
        }).AllowAnonymous().WithTags("Client");
}
