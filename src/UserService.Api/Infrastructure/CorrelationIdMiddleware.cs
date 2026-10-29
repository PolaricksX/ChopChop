using System.Text.RegularExpressions;

namespace UserService.Api.Infrastructure;

/// <summary>Reads or generates <c>X-Correlation-Id</c>, echoes it, and adds it to the logging scope.</summary>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";

    [GeneratedRegex(@"^[A-Za-z0-9\-_.]{1,64}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex ValidId();

    public async Task InvokeAsync(HttpContext context)
    {
        var supplied = context.Request.Headers[HeaderName].FirstOrDefault();
        var id = supplied is not null && ValidId().IsMatch(supplied) ? supplied : Guid.NewGuid().ToString("N");

        context.Items[ItemKey] = id;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = id;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { [ItemKey] = id }))
        {
            await next(context);
        }
    }

    public static string? Get(HttpContext context) => context.Items[ItemKey] as string;
}
