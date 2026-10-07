using System.Threading.RateLimiting;
using Aegi.Vas.Dashboard.Api.Configuration;
using Aegi.Vas.Dashboard.Api.Endpoints;
using Aegi.Vas.Dashboard.Api.Health;
using Aegi.Vas.Dashboard.Api.Middleware;
using Aegi.Vas.Dashboard.Api.Reporting;
using Aegi.Vas.Dashboard.Api.Security;
using Aegi.Vas.Dashboard.Api.Services;
using Azure.Identity;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
var env = builder.Environment;

// Key Vault: App Service managed identity reads only the Fabric workload credential (secret "Fabric--ClientSecret").
var keyVaultUri = config["KeyVault:Uri"];
if (!string.IsNullOrWhiteSpace(keyVaultUri) && !keyVaultUri.Contains('<'))
    config.AddAzureKeyVault(new Uri(keyVaultUri), new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned));

builder.Services.Configure<AuthenticationSettings>(config.GetSection(AuthenticationSettings.Section));
builder.Services.Configure<ReportingSettings>(config.GetSection(ReportingSettings.Section));
builder.Services.Configure<FabricSettings>(config.GetSection(FabricSettings.Section));

var reporting = config.GetSection(ReportingSettings.Section).Get<ReportingSettings>() ?? new();
if (reporting.Source == ReportingSource.Sample)
{
    if (!env.IsDevelopment())
        throw new InvalidOperationException("Reporting:Source=Sample is only allowed in the Development environment.");
    builder.Services.AddSingleton<IReportingDataSource, SampleDataSource>();
}
else
{
    builder.Services.AddSingleton<IReportingDataSource, FabricSqlDataSource>();
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<KpiService>();
builder.Services.AddMemoryCache();
builder.Services.AddDashboardAuthentication(config, env);
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = c =>
    c.ProblemDetails.Extensions["correlationId"] = c.HttpContext.TraceIdentifier);
builder.Services.AddHealthChecks().AddCheck<ReportingSourceHealthCheck>("reporting-source", tags: ["ready"]);

var permits = config.GetValue("RateLimiting:PermitsPerMinute", 120);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("per-user", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.User.FindFirst("oid")?.Value ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = TimeSpan.FromMinutes(1) }));
});

// Application Insights: health, failures and request/dependency (SQL) latency. Off when no connection string.
if (!string.IsNullOrWhiteSpace(config["ApplicationInsights:ConnectionString"]))
    builder.Services.AddApplicationInsightsTelemetry();

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    // App Service terminates TLS in front of the app and forwards the original scheme.
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseExceptionHandler(errorApp => errorApp.Run(async ctx =>
{
    // Never return stack traces or connection details. Fabric outages become a clear 503.
    var error = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    var unavailable = error is ReportingUnavailableException;
    ctx.Response.StatusCode = unavailable ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status500InternalServerError;
    await ctx.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new()
    {
        HttpContext = ctx,
        ProblemDetails =
        {
            Title = unavailable ? "Reporting data is temporarily unavailable." : "Something went wrong.",
            Detail = "Please try again. If it keeps happening, share the correlation ID with support.",
            Status = ctx.Response.StatusCode,
        },
    });
}));
app.UseStatusCodePages();
if (!env.IsDevelopment()) app.UseHsts();
app.UseHttpsRedirection();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });
app.MapClientConfig();
app.MapReportingEndpoints();
app.MapFallback("/api/{**path}", () => Results.Problem(statusCode: 404, title: "Not found."));
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
