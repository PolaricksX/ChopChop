namespace UserService.Api.Infrastructure;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Cache-Control"] = "no-store";

            // Swagger UI (development only) needs inline scripts and styles.
            if (!context.Request.Path.StartsWithSegments("/swagger"))
            {
                headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
            }

            return Task.CompletedTask;
        });

        return next(context);
    }
}
