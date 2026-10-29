using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using UserService.Domain.Exceptions;

namespace UserService.Api.Infrastructure;

/// <summary>Maps domain and framework exceptions to RFC 9457 problem details. Never returns stack traces or SQL.</summary>
public sealed class ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499;
            return true;
        }

        ProblemDetails problem = exception switch
        {
            DomainValidationException v => new HttpValidationProblemDetails(v.Errors.ToDictionary(e => e.Key, e => e.Value))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred.",
            },
            UserNotFoundException => Create(StatusCodes.Status404NotFound, "Not found", exception.Message),
            DuplicateUserException => Create(StatusCodes.Status409Conflict, "Conflict", exception.Message),
            ConflictException => Create(StatusCodes.Status409Conflict, "Conflict", exception.Message),
            BadHttpRequestException b => Create(b.StatusCode, "Bad request", "The request could not be processed."),
            _ => Create(StatusCodes.Status500InternalServerError, "An unexpected error occurred.", null),
        };

        if (problem.Status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path.Value);
        }

        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ProblemDetails Create(int status, string title, string? detail) =>
        new() { Status = status, Title = title, Detail = detail };
}

public static class ProblemDetailsExtensions
{
    /// <summary>Adds the correlation ID to every problem response.</summary>
    public static void AddCorrelation(this ProblemDetailsContext context)
    {
        var id = CorrelationIdMiddleware.Get(context.HttpContext);
        if (id is not null)
        {
            context.ProblemDetails.Extensions["correlationId"] = id;
        }

        context.ProblemDetails.Extensions.Remove("exception");
    }
}
