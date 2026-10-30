using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MealPlanner.Api.Contracts;
using MealPlanner.Data.Events;
using MealPlanner.Data.Repositories;
using MealPlanner.Domain.Entities;
using MealPlanner.Domain.Enums;
using MealPlanner.Domain.Exceptions;

namespace MealPlanner.Api.Services;

public sealed class MealPlanService(
    IUnitOfWork unitOfWork,
    IEventBus eventBus,
    TimeProvider clock,
    ILogger<MealPlanService> logger) : IMealPlanService
{
    private const string PlanResourceType = "Plan";
    private const string EntryResourceType = "Entry";

    public async Task<MealPlanPageResponse> ListAsync(
        string ownerId,
        int page,
        int pageSize,
        CancellationToken token)
    {
        var (items, totalCount) = await unitOfWork.CustomRepository<IMealPlanRepository>()
            .ListOwnedAsync(ownerId, (page - 1) * pageSize, pageSize, token);
        return new MealPlanPageResponse(items.Select(ToResponse).ToArray(), page, pageSize, totalCount);
    }

    public async Task<MealPlanResponse> GetAsync(string ownerId, Guid planId, CancellationToken token) =>
        ToResponse(await GetOwnedAsync(ownerId, planId, false, token));

    public async Task<MealPlanResponse> CreateAsync(
        string ownerId,
        CreateMealPlanRequest request,
        string idempotencyKey,
        string correlationId,
        CancellationToken token)
    {
        var records = unitOfWork.Repository<MealPlanIdempotencyRecord>();
        var fingerprint = Fingerprint(request);
        var existing = await records.GetAsync(
            record => record.OwnerId == ownerId && record.Key == idempotencyKey,
            token);
        if (existing is not null)
        {
            EnsureReplayMatches(existing, fingerprint, PlanResourceType, null);
            var replay = await unitOfWork.CustomRepository<IMealPlanRepository>()
                .GetOwnedAsync(existing.ResourceId, ownerId, false, token);
            return replay is null
                ? throw new ConflictException("The resource created by this Idempotency-Key no longer exists.")
                : ToResponse(replay);
        }

        var now = clock.GetUtcNow();
        var plan = MealPlan.Create(ownerId, request.Name!, request.StartDate, request.EndDate, now);
        await unitOfWork.Repository<MealPlan>().AddAsync(plan, token);
        await records.AddAsync(
            MealPlanIdempotencyRecord.Create(
                ownerId,
                idempotencyKey,
                fingerprint,
                PlanResourceType,
                plan.Id,
                null,
                now),
            token);
        await PublishChangedAsync(plan, "created", null, null, correlationId, now, token);
        await unitOfWork.SaveChangesAsync(token);

        logger.LogInformation("Meal plan {MealPlanId} created", plan.Id);
        return ToResponse(plan);
    }

    public async Task<MealPlanResponse> UpdateAsync(
        string ownerId,
        Guid planId,
        UpdateMealPlanRequest request,
        string correlationId,
        CancellationToken token)
    {
        var plan = await GetOwnedAsync(ownerId, planId, true, token);
        var version = DecodeVersion(request.Version);
        var now = clock.GetUtcNow();
        var repository = unitOfWork.CustomRepository<IMealPlanRepository>();
        var existingEntriesFitRange = !await repository.AnyEntryOutsideRangeAsync(
            plan.Id,
            request.StartDate,
            request.EndDate,
            token);
        plan.Update(request.Name!, request.StartDate, request.EndDate, existingEntriesFitRange, now);
        repository.SetExpectedVersion(plan, version);
        await PublishChangedAsync(plan, "updated", null, null, correlationId, now, token);
        await unitOfWork.SaveChangesAsync(token);
        logger.LogInformation("Meal plan {MealPlanId} updated", plan.Id);
        return ToResponse(plan);
    }

    public async Task<MealPlanResponse> ChangeStatusAsync(
        string ownerId,
        Guid planId,
        ChangeMealPlanStatusRequest request,
        string correlationId,
        CancellationToken token)
    {
        var plan = await GetOwnedAsync(ownerId, planId, true, token);
        var version = DecodeVersion(request.Version);
        var now = clock.GetUtcNow();
        plan.ChangeStatus(request.Status, now);
        unitOfWork.CustomRepository<IMealPlanRepository>().SetExpectedVersion(plan, version);
        await PublishChangedAsync(plan, "status-changed", null, request.Status.ToString(), correlationId, now, token);
        await unitOfWork.SaveChangesAsync(token);
        logger.LogInformation("Meal plan {MealPlanId} status changed to {Status}", plan.Id, plan.Status);
        return ToResponse(plan);
    }

    public async Task DeleteAsync(
        string ownerId,
        Guid planId,
        string version,
        string correlationId,
        CancellationToken token)
    {
        var plan = await GetOwnedAsync(ownerId, planId, true, token);
        var now = clock.GetUtcNow();
        unitOfWork.CustomRepository<IMealPlanRepository>().SetExpectedVersion(plan, DecodeVersion(version));
        await PublishChangedAsync(plan, "deleted", null, null, correlationId, now, token);
        unitOfWork.CustomRepository<IMealPlanRepository>().Delete(plan);
        await unitOfWork.SaveChangesAsync(token);
        logger.LogInformation("Meal plan {MealPlanId} deleted", plan.Id);
    }

    public async Task<MealEntryPageResponse> ListEntriesAsync(
        string ownerId,
        Guid planId,
        DateOnly? date,
        int page,
        int pageSize,
        CancellationToken token)
    {
        var plan = await GetOwnedAsync(ownerId, planId, false, token);
        var (items, totalCount) = await unitOfWork.CustomRepository<IMealPlanRepository>()
            .ListEntriesOwnedAsync(planId, ownerId, date, (page - 1) * pageSize, pageSize, token);
        return new MealEntryPageResponse(
            items.Select(entry => ToResponse(entry, plan.RowVersion)).ToArray(),
            page,
            pageSize,
            totalCount);
    }

    public async Task<MealEntryResponse> AddEntryAsync(
        string ownerId,
        Guid planId,
        CreateMealEntryRequest request,
        string idempotencyKey,
        string correlationId,
        CancellationToken token)
    {
        var repository = unitOfWork.CustomRepository<IMealPlanRepository>();
        var plan = await GetOwnedAsync(ownerId, planId, true, token);
        var records = unitOfWork.Repository<MealPlanIdempotencyRecord>();
        var fingerprint = Fingerprint(request);
        var existing = await records.GetAsync(
            record => record.OwnerId == ownerId && record.Key == idempotencyKey,
            token);
        if (existing is not null)
        {
            EnsureReplayMatches(existing, fingerprint, EntryResourceType, planId);
            var replay = await repository.GetEntryOwnedAsync(planId, existing.ResourceId, ownerId, false, token);
            return replay is null
                ? throw new ConflictException("The meal entry created by this Idempotency-Key no longer exists.")
                : ToResponse(replay, plan.RowVersion);
        }

        var version = DecodeVersion(request.Version);
        var now = clock.GetUtcNow();
        var slotAlreadyExists = await repository.SlotExistsAsync(planId, request.Date, request.Slot, null, token);
        var entry = plan.AddEntry(
            request.Date,
            request.Slot,
            request.Title!,
            request.RecipeId,
            request.Servings,
            slotAlreadyExists,
            now);
        await unitOfWork.Repository<MealEntry>().AddAsync(entry, token);
        repository.SetExpectedVersion(plan, version);
        await records.AddAsync(
            MealPlanIdempotencyRecord.Create(
                ownerId,
                idempotencyKey,
                fingerprint,
                EntryResourceType,
                entry.Id,
                planId,
                now),
            token);
        await PublishChangedAsync(plan, "meal-added", entry.Id, entry.Status.ToString(), correlationId, now, token);
        await unitOfWork.SaveChangesAsync(token);
        logger.LogInformation("Meal entry {MealEntryId} added to plan {MealPlanId}", entry.Id, plan.Id);
        return ToResponse(entry, plan.RowVersion);
    }

    public async Task<MealEntryResponse> UpdateEntryAsync(
        string ownerId,
        Guid planId,
        Guid entryId,
        UpdateMealEntryRequest request,
        string correlationId,
        CancellationToken token)
    {
        var plan = await GetOwnedAsync(ownerId, planId, true, token);
        var version = DecodeVersion(request.Version);
        var now = clock.GetUtcNow();
        var repository = unitOfWork.CustomRepository<IMealPlanRepository>();
        var entry = await GetEntryOwnedAsync(repository, planId, entryId, ownerId, true, token);
        var slotAlreadyExists = await repository.SlotExistsAsync(planId, request.Date, request.Slot, entryId, token);
        plan.UpdateEntry(entry, request.Date, request.Slot, request.Title!, request.RecipeId, request.Servings, slotAlreadyExists, now);
        repository.SetExpectedVersion(plan, version);
        await PublishChangedAsync(plan, "meal-updated", entryId, null, correlationId, now, token);
        await unitOfWork.SaveChangesAsync(token);
        logger.LogInformation("Meal entry {MealEntryId} updated in plan {MealPlanId}", entryId, plan.Id);
        return ToResponse(entry, plan.RowVersion);
    }

    public async Task<MealEntryResponse> ChangeEntryStatusAsync(
        string ownerId,
        Guid planId,
        Guid entryId,
        ChangeMealEntryStatusRequest request,
        string correlationId,
        CancellationToken token)
    {
        var plan = await GetOwnedAsync(ownerId, planId, true, token);
        var version = DecodeVersion(request.Version);
        var now = clock.GetUtcNow();
        var repository = unitOfWork.CustomRepository<IMealPlanRepository>();
        var entry = await GetEntryOwnedAsync(repository, planId, entryId, ownerId, true, token);
        plan.ChangeEntryStatus(entry, request.Status, now);
        repository.SetExpectedVersion(plan, version);
        await PublishChangedAsync(plan, "meal-status-changed", entryId, request.Status.ToString(), correlationId, now, token);
        await unitOfWork.SaveChangesAsync(token);
        logger.LogInformation("Meal entry {MealEntryId} status changed to {Status}", entryId, request.Status);
        return ToResponse(entry, plan.RowVersion);
    }

    public async Task RemoveEntryAsync(
        string ownerId,
        Guid planId,
        Guid entryId,
        string version,
        string correlationId,
        CancellationToken token)
    {
        var plan = await GetOwnedAsync(ownerId, planId, true, token);
        var now = clock.GetUtcNow();
        var repository = unitOfWork.CustomRepository<IMealPlanRepository>();
        var entry = await GetEntryOwnedAsync(repository, planId, entryId, ownerId, true, token);
        plan.RemoveEntry(entry, now);
        repository.SetExpectedVersion(plan, DecodeVersion(version));
        repository.DeleteEntry(entry);
        await PublishChangedAsync(plan, "meal-removed", entryId, null, correlationId, now, token);
        await unitOfWork.SaveChangesAsync(token);
        logger.LogInformation("Meal entry {MealEntryId} removed from plan {MealPlanId}", entryId, plan.Id);
    }

    private async Task<MealPlan> GetOwnedAsync(string ownerId, Guid planId, bool forUpdate, CancellationToken token) =>
        await unitOfWork.CustomRepository<IMealPlanRepository>().GetOwnedAsync(planId, ownerId, forUpdate, token)
        ?? throw new KeyNotFoundException("Meal plan was not found.");

    private static async Task<MealEntry> GetEntryOwnedAsync(
        IMealPlanRepository repository,
        Guid planId,
        Guid entryId,
        string ownerId,
        bool forUpdate,
        CancellationToken token) =>
        await repository.GetEntryOwnedAsync(planId, entryId, ownerId, forUpdate, token)
        ?? throw new KeyNotFoundException("Meal entry was not found.");

    private async Task PublishChangedAsync(
        MealPlan plan,
        string changeType,
        Guid? entryId,
        string? status,
        string correlationId,
        DateTimeOffset now,
        CancellationToken token) =>
        await eventBus.PublishAsync(
            new MealPlanEvent(
                Guid.NewGuid(),
                "MealPlanChanged",
                1,
                now,
                "MealPlanner",
                correlationId,
                new MealPlanChangedPayload(plan.Id, plan.OwnerId, changeType, entryId, status)),
            token);

    private static MealPlanResponse ToResponse(MealPlan plan) =>
        new(plan.Id, plan.Name, plan.StartDate, plan.EndDate, plan.Status, plan.CreatedAt, plan.UpdatedAt, VersionToken.Encode(plan.RowVersion));

    private static MealEntryResponse ToResponse(MealEntry entry, byte[] version) =>
        new(
            entry.Id,
            entry.MealPlanId,
            entry.Date,
            entry.Slot,
            entry.Title,
            entry.RecipeId,
            entry.Servings,
            entry.Status,
            entry.CreatedAt,
            entry.UpdatedAt,
            VersionToken.Encode(version));

    private static byte[] DecodeVersion(string? version) =>
        VersionToken.TryDecode(version, out var decoded)
            ? decoded
            : throw new DomainValidationException(new Dictionary<string, string[]>
            {
                ["version"] = ["A valid version token from the last response is required."],
            });

    private static string Fingerprint<T>(T request) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))));

    private static void EnsureReplayMatches(
        MealPlanIdempotencyRecord record,
        string fingerprint,
        string resourceType,
        Guid? parentId)
    {
        if (!string.Equals(record.RequestHash, fingerprint, StringComparison.Ordinal)
            || record.ResourceType != resourceType
            || record.ParentId != parentId)
        {
            throw new ConflictException("The Idempotency-Key was already used with a different request.");
        }
    }

    private sealed record MealPlanChangedPayload(
        Guid MealPlanId,
        string OwnerId,
        string ChangeType,
        Guid? MealEntryId,
        string? Status);
}
