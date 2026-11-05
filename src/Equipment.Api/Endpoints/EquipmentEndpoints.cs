using Equipment.Api.Contracts;
using Equipment.Api.Infrastructure;
using Equipment.Api.Services;
using Equipment.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Equipment.Api.Endpoints;

public static class EquipmentEndpoints
{
    public static IEndpointRouteBuilder MapEquipmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/equipment").WithTags("Equipment");

        group.MapPost("", CreateAsync)
            .RequireAuthorization(EquipmentPolicies.Write)
            .WithName("CreateEquipment")
            .WithSummary("Creates caller-owned equipment; requires an Idempotency-Key and equipment.write.")
            .Produces<EquipmentResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet("", ListAsync)
            .RequireAuthorization(EquipmentPolicies.Read)
            .WithName("ListEquipment")
            .WithSummary("Lists caller-owned equipment with bounded pagination and filters; requires equipment.read.")
            .Produces<EquipmentPageResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPut("/{equipmentId:guid}", UpdateAsync)
            .RequireAuthorization(EquipmentPolicies.Write)
            .WithName("UpdateEquipment")
            .WithSummary("Replaces equipment details using the current ETag; requires equipment.write.")
            .Produces<EquipmentResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPut("/{equipmentId:guid}/availability", SetAvailabilityAsync)
            .RequireAuthorization(EquipmentPolicies.Write)
            .WithName("SetEquipmentAvailability")
            .WithSummary("Sets equipment availability using the current ETag; requires equipment.write.")
            .Produces<EquipmentResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapDelete("/{equipmentId:guid}", DeleteAsync)
            .RequireAuthorization(EquipmentPolicies.Write)
            .WithName("DeleteEquipment")
            .WithSummary("Deletes caller-owned equipment using the current ETag; requires equipment.write.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        EquipmentUpsertRequest? request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        ICurrentUser user,
        EquipmentApplicationService service,
        HttpContext context,
        CancellationToken token)
    {
        var errors = EquipmentRequestValidator.ValidateCreate(request, idempotencyKey);
        if (errors.Count > 0)
        {
            return ValidationProblem(errors, context);
        }

        var response = await service.CreateAsync(
            user.Id,
            request!,
            idempotencyKey!,
            CorrelationIdMiddleware.Get(context)!,
            token);
        SetETag(context, response);
        return Results.Created($"/api/v1/equipment/{response.Id}", response);
    }

    private static async Task<IResult> ListAsync(
        int? page,
        int? pageSize,
        string? search,
        EquipmentType? type,
        EquipmentCondition? condition,
        EquipmentAvailability? availability,
        ICurrentUser user,
        EquipmentApplicationService service,
        HttpContext context,
        CancellationToken token)
    {
        var validatedPage = page ?? 1;
        var validatedPageSize = pageSize ?? EquipmentRequestValidator.DefaultPageSize;
        var errors = EquipmentRequestValidator.ValidateList(
            validatedPage,
            validatedPageSize,
            search,
            type,
            condition,
            availability);
        if (errors.Count > 0)
        {
            return ValidationProblem(errors, context);
        }

        return Results.Ok(await service.ListAsync(
            user.Id,
            new EquipmentListRequest(
                validatedPage,
                validatedPageSize,
                search,
                type,
                condition,
                availability),
            token));
    }

    private static async Task<IResult> UpdateAsync(
        Guid equipmentId,
        EquipmentUpsertRequest? request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        ICurrentUser user,
        EquipmentApplicationService service,
        HttpContext context,
        CancellationToken token)
    {
        var errors = EquipmentRequestValidator.ValidateUpdate(request, ifMatch);
        if (errors.Count > 0)
        {
            return ValidationProblem(errors, context);
        }

        var response = await service.UpdateAsync(
            user.Id,
            equipmentId,
            request!,
            ifMatch!,
            CorrelationIdMiddleware.Get(context)!,
            token);
        SetETag(context, response);
        return Results.Ok(response);
    }

    private static async Task<IResult> SetAvailabilityAsync(
        Guid equipmentId,
        EquipmentAvailabilityRequest? request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        ICurrentUser user,
        EquipmentApplicationService service,
        HttpContext context,
        CancellationToken token)
    {
        var errors = EquipmentRequestValidator.ValidateAvailability(request, ifMatch);
        if (errors.Count > 0)
        {
            return ValidationProblem(errors, context);
        }

        var response = await service.SetAvailabilityAsync(
            user.Id,
            equipmentId,
            request!.IsAvailable!.Value,
            ifMatch!,
            CorrelationIdMiddleware.Get(context)!,
            token);
        SetETag(context, response);
        return Results.Ok(response);
    }

    private static async Task<IResult> DeleteAsync(
        Guid equipmentId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        ICurrentUser user,
        EquipmentApplicationService service,
        HttpContext context,
        CancellationToken token)
    {
        var errors = EquipmentRequestValidator.ValidateDelete(ifMatch);
        if (errors.Count > 0)
        {
            return ValidationProblem(errors, context);
        }

        await service.DeleteAsync(
            user.Id,
            equipmentId,
            ifMatch!,
            CorrelationIdMiddleware.Get(context)!,
            token);
        return Results.NoContent();
    }

    private static IResult ValidationProblem(
        IReadOnlyDictionary<string, string[]> errors,
        HttpContext context) =>
        Results.ValidationProblem(
            errors,
            extensions: new Dictionary<string, object?>
            {
                ["correlationId"] = CorrelationIdMiddleware.Get(context),
            });

    private static void SetETag(HttpContext context, EquipmentResponse response) =>
        context.Response.Headers.ETag = $"\"{response.Version}\"";
}
