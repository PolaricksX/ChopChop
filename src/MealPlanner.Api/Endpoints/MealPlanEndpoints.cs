using MealPlanner.Api.Contracts;
using MealPlanner.Api.Infrastructure;
using MealPlanner.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MealPlanner.Api.Endpoints;

public static class MealPlanEndpoints
{
    public static IEndpointRouteBuilder MapMealPlanEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/meal-plans").WithTags("Meal plans");

        group.MapPost("/", CreateAsync)
            .RequireAuthorization(Policies.MealPlansWrite)
            .WithName("CreateMealPlan")
            .WithSummary("Creates a caller-owned meal plan; requires Idempotency-Key and mealplans.write.")
            .Produces<MealPlanResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet("/", ListAsync)
            .RequireAuthorization(Policies.MealPlansRead)
            .WithName("ListMealPlans")
            .WithSummary("Lists the caller's meal plans with bounded pagination; requires mealplans.read.")
            .Produces<MealPlanPageResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet("/{planId:guid}", GetAsync)
            .RequireAuthorization(Policies.MealPlansRead)
            .WithName("GetMealPlan")
            .WithSummary("Gets a caller-owned meal plan; requires mealplans.read and returns 404 for other users' plans.")
            .Produces<MealPlanResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPut("/{planId:guid}", UpdateAsync)
            .RequireAuthorization(Policies.MealPlansWrite)
            .WithName("UpdateMealPlan")
            .WithSummary("Replaces plan details using the last response's version token; requires mealplans.write.")
            .Produces<MealPlanResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPatch("/{planId:guid}/status", ChangeStatusAsync)
            .RequireAuthorization(Policies.MealPlansWrite)
            .WithName("ChangeMealPlanStatus")
            .WithSummary("Changes plan lifecycle status using an allowed transition and current version; requires mealplans.write.")
            .Produces<MealPlanResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapDelete("/{planId:guid}", DeleteAsync)
            .RequireAuthorization(Policies.MealPlansWrite)
            .WithName("DeleteMealPlan")
            .WithSummary("Deletes a caller-owned plan and its entries; requires mealplans.write and the latest ETag in If-Match.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet("/{planId:guid}/meals", ListEntriesAsync)
            .RequireAuthorization(Policies.MealPlansRead)
            .WithName("ListMealEntries")
            .WithSummary("Lists entries with pagination and optional calendar-date filter; requires mealplans.read.")
            .Produces<MealEntryPageResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/{planId:guid}/meals", AddEntryAsync)
            .RequireAuthorization(Policies.MealPlansWrite)
            .WithName("AddMealEntry")
            .WithSummary("Adds a planned meal; requires Idempotency-Key, a current plan version, and mealplans.write.")
            .Produces<MealEntryResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPut("/{planId:guid}/meals/{entryId:guid}", UpdateEntryAsync)
            .RequireAuthorization(Policies.MealPlansWrite)
            .WithName("UpdateMealEntry")
            .WithSummary("Replaces a planned meal using the current plan version; requires mealplans.write.")
            .Produces<MealEntryResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPatch("/{planId:guid}/meals/{entryId:guid}/status", ChangeEntryStatusAsync)
            .RequireAuthorization(Policies.MealPlansWrite)
            .WithName("ChangeMealEntryStatus")
            .WithSummary("Records meal progress with a validated status transition; requires mealplans.write.")
            .Produces<MealEntryResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapDelete("/{planId:guid}/meals/{entryId:guid}", RemoveEntryAsync)
            .RequireAuthorization(Policies.MealPlansWrite)
            .WithName("RemoveMealEntry")
            .WithSummary("Removes a planned meal using the latest plan ETag; requires mealplans.write.")
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
        CreateMealPlanRequest? request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        ICurrentUser user,
        IMealPlanService plans,
        HttpContext context,
        CancellationToken token)
    {
        var errors = MealPlanRequestValidator.Validate(request, idempotencyKey);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var plan = await plans.CreateAsync(
            user.Id,
            request!,
            idempotencyKey!,
            CorrelationIdMiddleware.Get(context)!,
            token);
        context.Response.Headers.ETag = Quote(plan.Version);
        return Results.Created($"/api/v1/meal-plans/{plan.Id}", plan);
    }

    private static async Task<IResult> ListAsync(
        int? page,
        int? pageSize,
        ICurrentUser user,
        IMealPlanService plans,
        CancellationToken token)
    {
        var currentPage = page ?? 1;
        var currentPageSize = pageSize ?? MealPlanRequestValidator.DefaultPageSize;
        var errors = MealPlanRequestValidator.ValidatePage(currentPage, currentPageSize);
        return errors.Count > 0
            ? Results.ValidationProblem(errors)
            : Results.Ok(await plans.ListAsync(user.Id, currentPage, currentPageSize, token));
    }

    private static async Task<IResult> GetAsync(
        Guid planId,
        ICurrentUser user,
        IMealPlanService plans,
        HttpContext context,
        CancellationToken token)
    {
        var plan = await plans.GetAsync(user.Id, planId, token);
        context.Response.Headers.ETag = Quote(plan.Version);
        return Results.Ok(plan);
    }

    private static async Task<IResult> UpdateAsync(
        Guid planId,
        UpdateMealPlanRequest? request,
        ICurrentUser user,
        IMealPlanService plans,
        HttpContext context,
        CancellationToken token)
    {
        var errors = MealPlanRequestValidator.Validate(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var plan = await plans.UpdateAsync(user.Id, planId, request!, CorrelationIdMiddleware.Get(context)!, token);
        context.Response.Headers.ETag = Quote(plan.Version);
        return Results.Ok(plan);
    }

    private static async Task<IResult> ChangeStatusAsync(
        Guid planId,
        ChangeMealPlanStatusRequest? request,
        ICurrentUser user,
        IMealPlanService plans,
        HttpContext context,
        CancellationToken token)
    {
        var errors = MealPlanRequestValidator.Validate(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var plan = await plans.ChangeStatusAsync(user.Id, planId, request!, CorrelationIdMiddleware.Get(context)!, token);
        context.Response.Headers.ETag = Quote(plan.Version);
        return Results.Ok(plan);
    }

    private static async Task<IResult> DeleteAsync(
        Guid planId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        ICurrentUser user,
        IMealPlanService plans,
        HttpContext context,
        CancellationToken token)
    {
        var version = ReadIfMatch(ifMatch);
        if (!MealPlanRequestValidator.TryDecodeVersion(version, out _))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["If-Match"] = ["A valid ETag from the last response is required."],
            });
        }

        await plans.DeleteAsync(user.Id, planId, version!, CorrelationIdMiddleware.Get(context)!, token);
        return Results.NoContent();
    }

    private static async Task<IResult> ListEntriesAsync(
        Guid planId,
        DateOnly? date,
        int? page,
        int? pageSize,
        ICurrentUser user,
        IMealPlanService plans,
        CancellationToken token)
    {
        var currentPage = page ?? 1;
        var currentPageSize = pageSize ?? MealPlanRequestValidator.DefaultPageSize;
        var errors = MealPlanRequestValidator.ValidatePage(currentPage, currentPageSize);
        return errors.Count > 0
            ? Results.ValidationProblem(errors)
            : Results.Ok(await plans.ListEntriesAsync(user.Id, planId, date, currentPage, currentPageSize, token));
    }

    private static async Task<IResult> AddEntryAsync(
        Guid planId,
        CreateMealEntryRequest? request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        ICurrentUser user,
        IMealPlanService plans,
        HttpContext context,
        CancellationToken token)
    {
        var errors = MealPlanRequestValidator.Validate(request, idempotencyKey);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var entry = await plans.AddEntryAsync(
            user.Id,
            planId,
            request!,
            idempotencyKey!,
            CorrelationIdMiddleware.Get(context)!,
            token);
        context.Response.Headers.ETag = Quote(entry.Version);
        return Results.Created($"/api/v1/meal-plans/{planId}/meals/{entry.Id}", entry);
    }

    private static async Task<IResult> UpdateEntryAsync(
        Guid planId,
        Guid entryId,
        UpdateMealEntryRequest? request,
        ICurrentUser user,
        IMealPlanService plans,
        HttpContext context,
        CancellationToken token)
    {
        var errors = MealPlanRequestValidator.Validate(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var entry = await plans.UpdateEntryAsync(
            user.Id,
            planId,
            entryId,
            request!,
            CorrelationIdMiddleware.Get(context)!,
            token);
        context.Response.Headers.ETag = Quote(entry.Version);
        return Results.Ok(entry);
    }

    private static async Task<IResult> ChangeEntryStatusAsync(
        Guid planId,
        Guid entryId,
        ChangeMealEntryStatusRequest? request,
        ICurrentUser user,
        IMealPlanService plans,
        HttpContext context,
        CancellationToken token)
    {
        var errors = MealPlanRequestValidator.Validate(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var entry = await plans.ChangeEntryStatusAsync(
            user.Id,
            planId,
            entryId,
            request!,
            CorrelationIdMiddleware.Get(context)!,
            token);
        context.Response.Headers.ETag = Quote(entry.Version);
        return Results.Ok(entry);
    }

    private static async Task<IResult> RemoveEntryAsync(
        Guid planId,
        Guid entryId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        ICurrentUser user,
        IMealPlanService plans,
        HttpContext context,
        CancellationToken token)
    {
        var version = ReadIfMatch(ifMatch);
        if (!MealPlanRequestValidator.TryDecodeVersion(version, out _))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["If-Match"] = ["A valid ETag from the last response is required."],
            });
        }

        await plans.RemoveEntryAsync(
            user.Id,
            planId,
            entryId,
            version!,
            CorrelationIdMiddleware.Get(context)!,
            token);
        return Results.NoContent();
    }

    private static string Quote(string version) => $"\"{version}\"";

    private static string? ReadIfMatch(string? ifMatch)
    {
        var value = ifMatch?.Trim();
        return value is { Length: > 1 } && value[0] == '"' && value[^1] == '"'
            ? value[1..^1]
            : null;
    }
}
