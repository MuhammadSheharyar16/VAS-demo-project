using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;

namespace Aegi.Vas.Dashboard.Api.Tests;

public sealed class AccessTests(EntraFactory factory) : IClassFixture<EntraFactory>
{
    private const string Path = "/api/v1/executive-summary";

    private async Task<HttpStatusCode> Call(string? token)
    {
        var client = factory.CreateClient();
        if (token is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (await client.GetAsync(Path)).StatusCode;
    }

    [Fact]
    public async Task Assigned_executive_with_scope_is_allowed()
    {
        var status = await Call(EntraFactory.Token());
        Assert.True(status == HttpStatusCode.OK, $"{status}: {EntraFactory.LastFailure}");
    }
    [Fact] public async Task No_token_is_rejected() => Assert.Equal(HttpStatusCode.Unauthorized, await Call(null));
    [Fact] public async Task Garbage_token_is_rejected() => Assert.Equal(HttpStatusCode.Unauthorized, await Call("not-a-jwt"));
    [Fact] public async Task Expired_token_is_rejected() => Assert.Equal(HttpStatusCode.Unauthorized, await Call(EntraFactory.Token(lifetime: TimeSpan.FromMinutes(-5))));
    [Fact] public async Task Wrong_audience_is_rejected() => Assert.Equal(HttpStatusCode.Unauthorized, await Call(EntraFactory.Token(audience: "api://some-other-api")));
    [Fact] public async Task Wrong_issuer_is_rejected() => Assert.Equal(HttpStatusCode.Unauthorized, await Call(EntraFactory.Token(issuer: "https://login.microsoftonline.com/other-tenant/v2.0")));
    [Fact] public async Task Wrong_signing_key_is_rejected() => Assert.Equal(HttpStatusCode.Unauthorized, await Call(EntraFactory.Token(key: new RsaSecurityKey(RSA.Create(2048)) { KeyId = "test-key" })));
    [Fact] public async Task Missing_scope_is_forbidden() => Assert.Equal(HttpStatusCode.Forbidden, await Call(EntraFactory.Token(scope: null)));
    [Fact] public async Task Wrong_scope_is_forbidden() => Assert.Equal(HttpStatusCode.Forbidden, await Call(EntraFactory.Token(scope: "User.Read")));
    [Fact] public async Task Executive_without_role_is_forbidden() => Assert.Equal(HttpStatusCode.Forbidden, await Call(EntraFactory.Token(role: null)));

    [Fact]
    public async Task Client_config_exposes_only_public_sign_in_values()
    {
        var cfg = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/client-config");
        Assert.True(cfg.GetProperty("authEnabled").GetBoolean());
        Assert.Equal($"{EntraFactory.Audience}/Dashboard.Read", cfg.GetProperty("apiScope").GetString());
        Assert.DoesNotContain("secret", cfg.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class OutageTests(OutageFactory factory) : IClassFixture<OutageFactory>
{
    [Fact]
    public async Task Fabric_outage_returns_503_with_correlation_id()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", EntraFactory.Token());
        var res = await client.GetAsync("/api/v1/vas/revenue");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("correlationId", body);
        Assert.DoesNotContain("Exception", body);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
    }
}

public sealed class StartupGuardTests
{
    [Theory]
    [InlineData("Authentication:Mode", "DevelopmentBypass")]
    [InlineData("Reporting:Source", "Sample")]
    public void Development_shortcuts_are_refused_outside_development(string key, string value)
    {
        using var f = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Production");
            b.UseSetting("KeyVault:Uri", "");
            b.UseSetting("Authentication:TenantId", EntraFactory.Tenant);
            b.UseSetting("Authentication:ApiAudience", EntraFactory.Audience);
            b.UseSetting(key, value);
        });
        var ex = Assert.ThrowsAny<Exception>(() => f.CreateClient());
        Assert.Contains("only allowed in the Development environment", ex.ToString());
    }
}
