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
        "/api/v1/vas/revenue", "/api/v1/vas/visa-types", "/api/v1/vas/stages", "/api/v1/vas/centres",
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
    public async Task Summary_has_the_agreed_kpis_and_matches_the_volume_endpoint()
    {
        var summary = (await _client.GetFromJsonAsync<JsonElement>("/api/v1/executive-summary")).GetProperty("data");
        var volume = await _client.GetFromJsonAsync<JsonElement>("/api/v1/vas/volume");
        var kpis = summary.GetProperty("kpis").EnumerateArray().ToDictionary(k => k.GetProperty("key").GetString()!, k => k.GetProperty("value").GetDecimal());
        Assert.Equal(
            ["applications_received", "applications_processed", "applications_pending", "approval_rate", "rejection_rate",
             "closure_rate", "turnaround_digital", "turnaround_centre", "revenue"], kpis.Keys);
        Assert.Equal(volume.GetProperty("data").GetProperty("total").GetInt64(), (long)kpis["applications_received"]);
        Assert.Equal(kpis["applications_received"], kpis["applications_processed"] + kpis["applications_pending"]);
        Assert.Equal(volume.GetProperty("data").GetProperty("total").GetInt64(), summary.GetProperty("volume").GetProperty("total").GetInt64());
        // Requirements section 2: only Operations, Finance and Alfit are Coming Soon. CSAT is out of scope.
        Assert.Equal(["Operations", "Finance", "Alfit"], summary.GetProperty("comingSoon").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public async Task Summary_compares_with_the_previous_period_of_the_same_length()
    {
        var q3 = (await _client.GetFromJsonAsync<JsonElement>("/api/v1/executive-summary?from=2026-07&to=2026-09")).GetProperty("data");
        var q2 = (await _client.GetFromJsonAsync<JsonElement>("/api/v1/executive-summary?from=2026-04&to=2026-06")).GetProperty("data");
        Assert.Equal("2026-04", q3.GetProperty("comparedWith").GetProperty("from").GetString());
        Assert.Equal("2026-06", q3.GetProperty("comparedWith").GetProperty("to").GetString());

        JsonElement Kpi(JsonElement s, string key) => s.GetProperty("kpis").EnumerateArray().Single(k => k.GetProperty("key").GetString() == key);
        var received = Kpi(q3, "applications_received");
        var before = Kpi(q2, "applications_received").GetProperty("value").GetDecimal();
        Assert.Equal(before, received.GetProperty("previous").GetDecimal());
        Assert.Equal(Math.Round((received.GetProperty("value").GetDecimal() - before) / before * 100, 1), received.GetProperty("change").GetDecimal());
        Assert.Equal("percent", received.GetProperty("changeUnit").GetString());
        Assert.Equal("points", Kpi(q3, "approval_rate").GetProperty("changeUnit").GetString());
        Assert.Equal("days", Kpi(q3, "turnaround_centre").GetProperty("changeUnit").GetString());
    }

    [Fact]
    public async Task Summary_without_an_earlier_period_shows_no_comparison()
    {
        var ytd = (await _client.GetFromJsonAsync<JsonElement>("/api/v1/executive-summary")).GetProperty("data");
        Assert.Equal(JsonValueKind.Null, ytd.GetProperty("comparedWith").ValueKind);
        Assert.All(ytd.GetProperty("kpis").EnumerateArray(), k => Assert.Equal(JsonValueKind.Null, k.GetProperty("change").ValueKind));
    }

    [Fact]
    public async Task Kpi_status_follows_the_configured_thresholds()
    {
        // Development settings: approval_rate Watch 88, Poor 85 (higher is better); turnaround_centre Watch 3.5, Poor 5 (lower is better).
        var kpis = (await _client.GetFromJsonAsync<JsonElement>("/api/v1/executive-summary")).GetProperty("data").GetProperty("kpis").EnumerateArray().ToList();
        string? Expected(decimal v, decimal watch, decimal poor, bool higherIsBetter) =>
            higherIsBetter ? v < poor ? "poor" : v < watch ? "watch" : "good" : v > poor ? "poor" : v > watch ? "watch" : "good";
        var approval = kpis.Single(k => k.GetProperty("key").GetString() == "approval_rate");
        var centre = kpis.Single(k => k.GetProperty("key").GetString() == "turnaround_centre");
        Assert.Equal(Expected(approval.GetProperty("value").GetDecimal(), 88, 85, true), approval.GetProperty("status").GetString());
        Assert.Equal(Expected(centre.GetProperty("value").GetDecimal(), 3.5m, 5, false), centre.GetProperty("status").GetString());
        // No threshold agreed for volumes, so no status is invented.
        Assert.Equal(JsonValueKind.Null, kpis[0].GetProperty("status").ValueKind);
    }

    [Fact]
    public async Task Visa_type_filter_narrows_every_view()
    {
        long Total(JsonElement e) => e.GetProperty("data").GetProperty("total").GetInt64();
        var all = await _client.GetFromJsonAsync<JsonElement>("/api/v1/vas/volume");
        var work = await _client.GetFromJsonAsync<JsonElement>("/api/v1/vas/volume?visaType=work");
        Assert.True(Total(all) > Total(work) && Total(work) > 0);
        Assert.Equal("Work", work.GetProperty("meta").GetProperty("filters").GetProperty("visaType").GetString());

        var types = await _client.GetFromJsonAsync<JsonElement>("/api/v1/vas/visa-types?visaType=Work");
        Assert.Equal("Work", types.GetProperty("data").GetProperty("visaTypes").EnumerateArray().Single().GetProperty("visaType").GetString());
        var revenue = await _client.GetFromJsonAsync<JsonElement>("/api/v1/vas/revenue?visaType=Work");
        Assert.Equal("Work", revenue.GetProperty("data").GetProperty("byVisaType").EnumerateArray().Single().GetProperty("visaType").GetString());
    }

    [Fact]
    public async Task Revenue_breakdowns_add_up_to_the_total()
    {
        var data = (await _client.GetFromJsonAsync<JsonElement>("/api/v1/vas/revenue")).GetProperty("data");
        var total = data.GetProperty("total").GetDecimal();
        Assert.Equal(total, data.GetProperty("countries").EnumerateArray().Sum(c => c.GetProperty("revenue").GetDecimal()));
        Assert.Equal(total, data.GetProperty("byVisaType").EnumerateArray().Sum(c => c.GetProperty("revenue").GetDecimal()));
        Assert.Equal(6, data.GetProperty("byVisaType").GetArrayLength());
    }

    [Fact]
    public async Task Stages_are_ordered_and_never_grow()
    {
        var stages = (await _client.GetFromJsonAsync<JsonElement>("/api/v1/vas/stages")).GetProperty("data").GetProperty("stages").EnumerateArray().ToList();
        var outcomes = (await _client.GetFromJsonAsync<JsonElement>("/api/v1/vas/outcomes")).GetProperty("data");
        Assert.Equal(["Submitted", "In review", "Decided", "Closed"], stages.Select(s => s.GetProperty("stage").GetString()));
        Assert.Equal(outcomes.GetProperty("received").GetInt64(), stages[0].GetProperty("total").GetInt64());
        Assert.Equal(100m, stages[0].GetProperty("percentOfFirst").GetDecimal());
        var totals = stages.Select(s => s.GetProperty("total").GetInt64()).ToList();
        Assert.Equal(totals.OrderByDescending(t => t), totals);
        Assert.All(stages, s => Assert.Equal(s.GetProperty("total").GetInt64(), s.GetProperty("centre").GetInt64() + s.GetProperty("digital").GetInt64()));
    }

    [Fact]
    public async Task Centre_performance_covers_every_centre_and_hides_utilisation_for_a_visa_type()
    {
        var centres = (await _client.GetFromJsonAsync<JsonElement>("/api/v1/vas/centres")).GetProperty("data").GetProperty("centres").EnumerateArray().ToList();
        var volume = await _client.GetFromJsonAsync<JsonElement>("/api/v1/vas/volume");
        Assert.Equal(11, centres.Count);
        Assert.Equal(volume.GetProperty("data").GetProperty("total").GetInt64(), centres.Sum(c => c.GetProperty("applications").GetInt64()));
        Assert.All(centres, c =>
        {
            Assert.Equal(c.GetProperty("applications").GetInt64(), c.GetProperty("centreJourney").GetInt64() + c.GetProperty("digital").GetInt64());
            Assert.InRange(c.GetProperty("utilisationPercent").GetDecimal(), 1, 200);
            Assert.True(c.GetProperty("avgTurnaroundDays").GetDecimal() > 0);
        });

        var uae = (await _client.GetFromJsonAsync<JsonElement>("/api/v1/vas/centres?country=UAE&visaType=Work")).GetProperty("data").GetProperty("centres").EnumerateArray().ToList();
        Assert.Equal(2, uae.Count);
        Assert.All(uae, c => Assert.Equal(JsonValueKind.Null, c.GetProperty("utilisationPercent").ValueKind));
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
        Assert.Equal("approval_rate", kpis[3].GetProperty("key").GetString());
        Assert.Equal(JsonValueKind.Null, kpis[3].GetProperty("value").ValueKind);
    }

    [Theory]
    [InlineData("/api/v1/vas/volume?from=2026-13")]
    [InlineData("/api/v1/vas/volume?from=2026-05&to=2026-01")]
    [InlineData("/api/v1/vas/volume?from=2020-01&to=2026-09")]
    [InlineData("/api/v1/vas/volume?country=Atlantis")]
    [InlineData("/api/v1/vas/volume?centre=Dubai")]
    [InlineData("/api/v1/vas/volume?country=UAE&centre=Riyadh")]
    [InlineData("/api/v1/vas/volume?country=UAE';DROP TABLE x;--")]
    [InlineData("/api/v1/vas/volume?visaType=Diplomatic")]
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
        Assert.Contains("Tourist", res.GetProperty("visaTypes").EnumerateArray().Select(x => x.GetString()));
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
