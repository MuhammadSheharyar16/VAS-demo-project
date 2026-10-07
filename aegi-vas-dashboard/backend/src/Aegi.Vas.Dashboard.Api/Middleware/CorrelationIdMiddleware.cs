using System.Text.RegularExpressions;

namespace Aegi.Vas.Dashboard.Api.Middleware;

/// <summary>
/// Gives every request a correlation ID (accepts a well-formed X-Correlation-ID from the caller, otherwise
/// creates one), returns it in the response header and adds it to every log entry and error body.
/// </summary>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> log)
{
    public const string Header = "X-Correlation-ID";

    [GeneratedRegex("^[A-Za-z0-9-]{8,64}$")]
    private static partial Regex Safe();

    public async Task InvokeAsync(HttpContext ctx)
    {
        var incoming = ctx.Request.Headers[Header].ToString();
        var id = Safe().IsMatch(incoming) ? incoming : Guid.NewGuid().ToString("N");
        ctx.TraceIdentifier = id;
        ctx.Response.OnStarting(() => { ctx.Response.Headers[Header] = id; return Task.CompletedTask; });
        using (log.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = id }))
            await next(ctx);
    }
}
