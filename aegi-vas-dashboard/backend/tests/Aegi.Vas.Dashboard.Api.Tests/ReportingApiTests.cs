using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Aegi.Vas.Dashboard.Api.Tests;

public sealed class ReportingApiTests(DevelopmentFactory factory) : IClassFixture<DevelopmentFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    public static TheoryData<string> Endpoints =>
    [
        "/api/v1/executive-summary", "/api/v1/vas/volume", "/api/v1/vas/outcomes", "/api/v1/vas/closure",
        "/api/v1/vas/turnaround?journey=digital", "/api/v1/vas/turnaround?journey=centre",
        "/api/v1/vas/revenue", "/api/v1/vas/visa-types",
    ];

    [Theory, MemberData(nameof(Endpoints))]
    public async Task Every_endpoint_returns_data_with_meta(string path)
    {
        var res = await _client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var meta = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("meta");
        Assert.Equal("USD", meta.GetProperty("currency").GetString());
        Assert.Equal("2026-01", meta.GetProperty("filters").GetProperty("from").GetString());
        Assert.False(string.IsNullOrEmpty(meta.GetProperty("freshness").GetProperty("lastRefreshedUtc").GetString()));
        Assert.Equal(res.Headers.GetValues("X-Correlation-ID").Single(), meta.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task Summary_matches_volume_endpoint_and_has_six_kpis()
    {
        var summary = await _client.GetFromJsonAsync<JsonElement>("/api/v1/executive-summary");
        var volume = await _client.GetFromJsonAsync<JsonElement>("/api/v1/vas/volume");
        var kpis = summary.GetProperty("data").GetProperty("kpis").EnumerateArray().ToList();
        Assert.Equal(6, kpis.Count);
        Assert.Equal(volume.GetProperty("data").GetProperty("total").GetInt64(), kpis[0].GetProperty("value").GetInt64());
        Assert.Contains("CSAT", summary.GetProperty("data").GetProperty("comingSoon").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public async Task Country_and_centre_filters_narrow_results()
    {
        var all = await _client.GetFromJsonAsync<JsonElement>("/api/v1/vas/volume");
        var uae = await _client.GetFromJsonAsync<JsonElement>("/api/v1/vas/volume?country=uae");
        var dubai = await _client.GetFromJsonAsync<JsonElement>("/api/v1/vas/volume?country=UAE&centre=Dubai");
        long Total(JsonElement e) => e.GetProperty("data").GetProperty("total").GetInt64();
        Assert.True(Total(all) > Total(uae) && Total(uae) > Total(dubai) && Total(dubai) > 0);
        Assert.Equal("UAE", uae.GetProperty("meta").GetProperty("filters").GetProperty("country").GetString());
    }

    [Fact]
    public async Task Date_boundaries_are_inclusive()
    {
        var q3 = await _client.GetFromJsonAsync<JsonElement>("/api/v1/vas/volume?from=2026-07&to=2026-09");
        var months = q3.GetProperty("data").GetProperty("months").EnumerateArray().Select(m => m.GetProperty("month").GetString()).ToList();
        Assert.Equal(["2026-07", "2026-08", "2026-09"], months);
    }

    [Fact]
    public async Task Period_without_data_returns_empty_not_error()
    {
        var res = await _client.GetFromJsonAsync<JsonElement>("/api/v1/executive-summary?from=2025-01&to=2025-03");
        var kpis = res.GetProperty("data").GetProperty("kpis");
        Assert.Equal(0, kpis[0].GetProperty("value").GetInt64());
        Assert.Equal(JsonValueKind.Null, kpis[1].GetProperty("value").ValueKind);
    }

    [Theory]
    [InlineData("/api/v1/vas/volume?from=2026-13")]
    [InlineData("/api/v1/vas/volume?from=2026-05&to=2026-01")]
    [InlineData("/api/v1/vas/volume?from=2020-01&to=2026-09")]
    [InlineData("/api/v1/vas/volume?country=Atlantis")]
    [InlineData("/api/v1/vas/volume?centre=Dubai")]
    [InlineData("/api/v1/vas/volume?country=UAE&centre=Riyadh")]
    [InlineData("/api/v1/vas/volume?country=UAE';DROP TABLE x;--")]
    [InlineData("/api/v1/vas/turnaround?journey=both")]
    public async Task Invalid_filters_get_problem_details(string path)
    {
        var res = await _client.GetAsync(path);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("application/problem+json", res.Content.Headers.ContentType?.MediaType);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("errors", out _));
        Assert.True(body.TryGetProperty("correlationId", out _));
    }

    [Fact]
    public async Task Filter_options_list_countries_and_centres()
    {
        var res = await _client.GetFromJsonAsync<JsonElement>("/api/v1/filters");
        var uae = res.GetProperty("countries").EnumerateArray().Single(c => c.GetProperty("country").GetString() == "UAE");
        Assert.Contains("Dubai", uae.GetProperty("centres").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public async Task Security_headers_and_correlation_id_are_set()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/vas/outcomes");
        req.Headers.Add("X-Correlation-ID", "test-correlation-1234");
        var res = await _client.SendAsync(req);
        Assert.Equal("test-correlation-1234", res.Headers.GetValues("X-Correlation-ID").Single());
        Assert.Equal("nosniff", res.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("no-store", res.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Health_and_client_config_are_public()
    {
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/health/ready")).StatusCode);
        var cfg = await _client.GetFromJsonAsync<JsonElement>("/api/client-config");
        Assert.False(cfg.GetProperty("authEnabled").GetBoolean());
    }

    [Fact]
    public async Task Unknown_api_route_is_404_problem()
    {
        var res = await _client.GetAsync("/api/v1/nope");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
