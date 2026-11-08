using Equipment.Data.Exceptions;
using Equipment.Domain.Exceptions;
using Equipment.Api.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Equipment.Api.Infrastructure;

public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499;
            return true;
        }

        var problem = exception switch
        {
            EquipmentDomainException domain => Create(
                StatusCodes.Status400BadRequest,
                "Invalid request",
                domain.Message),
            ArgumentException argument => Create(
                StatusCodes.Status400BadRequest,
                "Invalid request",
                argument.Message),
            EquipmentDuplicateException => Create(
                StatusCodes.Status409Conflict,
                "Equipment conflict",
                exception.Message),
            EquipmentIdempotencyKeyException => Create(
                StatusCodes.Status409Conflict,
                "Idempotency conflict",
                exception.Message),
            EquipmentConcurrencyException => Create(
                StatusCodes.Status409Conflict,
                "Concurrency conflict",
                exception.Message),
            EquipmentIdempotencyConflictException => Create(
                StatusCodes.Status409Conflict,
                "Idempotency conflict",
                exception.Message),
            EquipmentIdempotencyResourceMissingException => Create(
                StatusCodes.Status409Conflict,
                "Idempotency conflict",
                exception.Message),
            KeyNotFoundException => Create(
                StatusCodes.Status404NotFound,
                "Not found",
                "The requested equipment item was not found."),
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
        var correlationId = CorrelationIdMiddleware.Get(context.HttpContext);
        if (correlationId is not null)
        {
            context.ProblemDetails.Extensions["correlationId"] = correlationId;
        }

        context.ProblemDetails.Extensions.Remove("exception");
    }
}
