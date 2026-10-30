using MealPlanner.Api.Contracts;
using MealPlanner.Domain.Enums;

namespace MealPlanner.Api.Services;

public interface IMealPlanService
{
    Task<MealPlanPageResponse> ListAsync(string ownerId, int page, int pageSize, CancellationToken token);

    Task<MealPlanResponse> GetAsync(string ownerId, Guid planId, CancellationToken token);

    Task<MealPlanResponse> CreateAsync(
        string ownerId,
        CreateMealPlanRequest request,
        string idempotencyKey,
        string correlationId,
        CancellationToken token);

    Task<MealPlanResponse> UpdateAsync(
        string ownerId,
        Guid planId,
        UpdateMealPlanRequest request,
        string correlationId,
        CancellationToken token);

    Task<MealPlanResponse> ChangeStatusAsync(
        string ownerId,
        Guid planId,
        ChangeMealPlanStatusRequest request,
        string correlationId,
        CancellationToken token);

    Task DeleteAsync(
        string ownerId,
        Guid planId,
        string version,
        string correlationId,
        CancellationToken token);

    Task<MealEntryPageResponse> ListEntriesAsync(
        string ownerId,
        Guid planId,
        DateOnly? date,
        int page,
        int pageSize,
        CancellationToken token);

    Task<MealEntryResponse> AddEntryAsync(
        string ownerId,
        Guid planId,
        CreateMealEntryRequest request,
        string idempotencyKey,
        string correlationId,
        CancellationToken token);

    Task<MealEntryResponse> UpdateEntryAsync(
        string ownerId,
        Guid planId,
        Guid entryId,
        UpdateMealEntryRequest request,
        string correlationId,
        CancellationToken token);

    Task<MealEntryResponse> ChangeEntryStatusAsync(
        string ownerId,
        Guid planId,
        Guid entryId,
        ChangeMealEntryStatusRequest request,
        string correlationId,
        CancellationToken token);

    Task RemoveEntryAsync(
        string ownerId,
        Guid planId,
        Guid entryId,
        string version,
        string correlationId,
        CancellationToken token);
}
