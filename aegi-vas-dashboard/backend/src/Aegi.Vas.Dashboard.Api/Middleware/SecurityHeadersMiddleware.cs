namespace Aegi.Vas.Dashboard.Api.Middleware;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    // The SPA is served from this origin; it talks to this API and to Entra ID for sign-in.
    private const string Csp =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com; " +
        "img-src 'self' data: blob:; " +
        "connect-src 'self' https://login.microsoftonline.com; " +
        "frame-src https://login.microsoftonline.com; " +
        "frame-ancestors 'none'; base-uri 'self'; form-action 'self' https://login.microsoftonline.com";

    public Task InvokeAsync(HttpContext ctx)
    {
        ctx.Response.OnStarting(() =>
        {
            var h = ctx.Response.Headers;
            h["X-Content-Type-Options"] = "nosniff";
            h["X-Frame-Options"] = "DENY";
            h["Referrer-Policy"] = "strict-origin-when-cross-origin";
            h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            h["Content-Security-Policy"] = Csp;
            if (ctx.Request.Path.StartsWithSegments("/api")) h["Cache-Control"] = "no-store";
            return Task.CompletedTask;
        });
        return next(ctx);
    }
}
