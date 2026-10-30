namespace MealPlanner.Api.Infrastructure;

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
            headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
            return Task.CompletedTask;
        });

        return next(context);
    }
}
