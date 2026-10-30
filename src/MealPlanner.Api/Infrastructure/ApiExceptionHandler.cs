using MealPlanner.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MealPlanner.Api.Infrastructure;

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
            DomainValidationException validation => new HttpValidationProblemDetails(
                validation.Errors.ToDictionary(error => error.Key, error => error.Value))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred.",
            },
            ConflictException => Create(StatusCodes.Status409Conflict, "Conflict", exception.Message),
            KeyNotFoundException => Create(StatusCodes.Status404NotFound, "Not found", exception.Message),
            BadHttpRequestException badRequest => Create(
                badRequest.StatusCode,
                "Bad request",
                "The request could not be processed."),
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
