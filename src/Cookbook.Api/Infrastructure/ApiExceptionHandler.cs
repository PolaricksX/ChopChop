using Cookbook.Api.Contracts;
using Cookbook.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cookbook.Api.Infrastructure;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            RequestValidationException validation => (400, "Validation failed", string.Join(" ", validation.Errors)),
            CookbookValidationException validation => (400, "Validation failed", validation.Message),
            CookbookNotFoundException => (404, "Resource not found", "The requested resource was not found."),
            KeyNotFoundException => (404, "Resource not found", "The requested resource was not found."),
            UnauthorizedAccessException => (401, "Unauthorized", "Authentication is required."),
            DbUpdateException => (500, "Internal server error", "The request could not be completed."),
            _ => (500, "Internal server error", "The request could not be completed.")
        };

        if (status >= 500)
            logger.LogError(exception, "Cookbook API request failed with status {StatusCode}", status);
        else
            logger.LogWarning("Cookbook API request failed with status {StatusCode}: {Detail}", status, detail);

        var correlationId = httpContext.Items[ChopChop.Shared.CorrelationIdMiddleware.ItemName]?.ToString()
            ?? httpContext.TraceIdentifier;
        var problem = new ProblemDetails
        {
            Type = $"https://httpstatuses.com/{status}",
            Title = title,
            Status = status,
            Detail = detail,
            Instance = httpContext.Request.Path
        };
        problem.Extensions["correlationId"] = correlationId;
        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
