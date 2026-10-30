using MealPlanner.Domain.Entities;

namespace MealPlanner.Data.Repositories;

public interface IMealPlanRepository : IRepository<MealPlan>
{
    Task<(IReadOnlyList<MealPlan> Items, int TotalCount)> ListOwnedAsync(
        string ownerId,
        int skip,
        int take,
        CancellationToken token);

    Task<MealPlan?> GetOwnedAsync(Guid id, string ownerId, bool forUpdate, CancellationToken token);

    Task<MealEntry?> GetEntryOwnedAsync(
        Guid planId,
        Guid entryId,
        string ownerId,
        bool forUpdate,
        CancellationToken token);

    Task<bool> SlotExistsAsync(Guid planId, DateOnly date, MealPlanner.Domain.Enums.MealSlot slot, Guid? excludingEntryId, CancellationToken token);

    Task<bool> AnyEntryOutsideRangeAsync(Guid planId, DateOnly startDate, DateOnly endDate, CancellationToken token);

    Task<(IReadOnlyList<MealEntry> Items, int TotalCount)> ListEntriesOwnedAsync(
        Guid planId,
        string ownerId,
        DateOnly? date,
        int skip,
        int take,
        CancellationToken token);

    void Delete(MealPlan plan);

    void DeleteEntry(MealEntry entry);

    void SetExpectedVersion(MealPlan plan, byte[] version);
}
