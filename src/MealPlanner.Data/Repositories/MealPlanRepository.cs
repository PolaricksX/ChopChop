using MealPlanner.Data.Context;
using MealPlanner.Domain.Entities;
using MealPlanner.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Data.Repositories;

public sealed class MealPlanRepository : EfRepository<MealPlan>, IMealPlanRepository
{
    private readonly MealPlannerDbContext context;

    public MealPlanRepository(MealPlannerDbContext context) : base(context) => this.context = context;

    public async Task<(IReadOnlyList<MealPlan> Items, int TotalCount)> ListOwnedAsync(
        string ownerId,
        int skip,
        int take,
        CancellationToken token)
    {
        var plans = context.MealPlans.AsNoTracking().Where(plan => plan.OwnerId == ownerId);
        var totalCount = await plans.CountAsync(token);
        var items = await plans
            .OrderByDescending(plan => plan.CreatedAt)
            .ThenBy(plan => plan.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(token);
        return (items, totalCount);
    }

    public Task<MealPlan?> GetOwnedAsync(Guid id, string ownerId, bool forUpdate, CancellationToken token)
    {
        var query = context.MealPlans
            .Where(plan => plan.Id == id && plan.OwnerId == ownerId)
            .AsQueryable();
        if (!forUpdate)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync(token);
    }

    public Task<MealEntry?> GetEntryOwnedAsync(
        Guid planId,
        Guid entryId,
        string ownerId,
        bool forUpdate,
        CancellationToken token)
    {
        var ownedPlanIds = context.MealPlans.Where(plan => plan.OwnerId == ownerId).Select(plan => plan.Id);
        var query = context.MealEntries
            .Where(entry => entry.MealPlanId == planId
                && entry.Id == entryId
                && ownedPlanIds.Contains(entry.MealPlanId))
            .AsQueryable();
        if (!forUpdate)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync(token);
    }

    public Task<bool> SlotExistsAsync(
        Guid planId,
        DateOnly date,
        MealSlot slot,
        Guid? excludingEntryId,
        CancellationToken token)
    {
        if (slot is MealSlot.Snack)
        {
            return Task.FromResult(false);
        }

        var entries = context.MealEntries.AsNoTracking()
            .Where(entry => entry.MealPlanId == planId && entry.Date == date && entry.Slot == slot);
        if (excludingEntryId.HasValue)
        {
            entries = entries.Where(entry => entry.Id != excludingEntryId.Value);
        }

        return entries.AnyAsync(token);
    }

    public Task<bool> AnyEntryOutsideRangeAsync(
        Guid planId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken token) =>
        context.MealEntries.AsNoTracking()
            .AnyAsync(entry => entry.MealPlanId == planId
                && (entry.Date < startDate || entry.Date > endDate), token);

    public async Task<(IReadOnlyList<MealEntry> Items, int TotalCount)> ListEntriesOwnedAsync(
        Guid planId,
        string ownerId,
        DateOnly? date,
        int skip,
        int take,
        CancellationToken token)
    {
        var ownedPlanIds = context.MealPlans.Where(plan => plan.OwnerId == ownerId).Select(plan => plan.Id);
        var entries = context.MealEntries.AsNoTracking()
            .Where(entry => entry.MealPlanId == planId && ownedPlanIds.Contains(entry.MealPlanId));
        if (date.HasValue)
        {
            entries = entries.Where(entry => entry.Date == date.Value);
        }

        var totalCount = await entries.CountAsync(token);
        var items = await entries
            .OrderBy(entry => entry.Date)
            .ThenBy(entry => entry.Slot)
            .ThenBy(entry => entry.CreatedAt)
            .ThenBy(entry => entry.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(token);
        return (items, totalCount);
    }

    public void Delete(MealPlan plan) => context.MealPlans.Remove(plan);

    public void DeleteEntry(MealEntry entry) => context.MealEntries.Remove(entry);

    public void SetExpectedVersion(MealPlan plan, byte[] version) =>
        context.Entry(plan).Property(item => item.RowVersion).OriginalValue = version;
}
