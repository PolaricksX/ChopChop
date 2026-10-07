using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace UserPantry.Api.Middleware;

public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not ArgumentOutOfRangeException)
        {
            return false;
        }

        logger.LogWarning("Request validation failed for {Path}: {ParameterName}", httpContext.Request.Path, ((ArgumentOutOfRangeException)exception).ParamName);

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid request parameters.",
            Detail = "One or more request parameters are outside the allowed range.",
            Instance = httpContext.Request.Path
        };
        problemDetails.Extensions["correlationId"] =
            httpContext.Items[CorrelationIdMiddleware.HeaderName]?.ToString();

        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails
        });

        return true;
    }
}
