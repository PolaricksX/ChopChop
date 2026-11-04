using MealPlanner.Domain.Enums;
using MealPlanner.Domain.Exceptions;
using MealPlanner.Domain.Services;

namespace MealPlanner.Domain.Entities;

public sealed class MealPlan
{
    private MealPlan()
    {
    }

    public Guid Id { get; private set; }

    public string OwnerId { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public MealPlanStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public static MealPlan Create(string ownerId, string name, DateOnly startDate, DateOnly endDate, DateTimeOffset now)
    {
        var errors = MealPlanRules.ValidatePlan(ownerId, name, startDate, endDate);
        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }

        return new MealPlan
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Name = name.Trim(),
            StartDate = startDate,
            EndDate = endDate,
            Status = MealPlanStatus.Draft,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public void Update(
        string name,
        DateOnly startDate,
        DateOnly endDate,
        bool existingEntriesFitRange,
        DateTimeOffset now)
    {
        EnsureEditable();

        var errors = MealPlanRules.ValidatePlan(OwnerId, name, startDate, endDate);
        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }

        if (!existingEntriesFitRange)
        {
            throw new DomainValidationException(new Dictionary<string, string[]>
            {
                ["dateRange"] = ["The updated plan range must include every existing meal entry."],
            });
        }

        Name = name.Trim();
        StartDate = startDate;
        EndDate = endDate;
        UpdatedAt = now;
    }

    public void ChangeStatus(MealPlanStatus status, DateTimeOffset now)
    {
        var allowed = Status switch
        {
            MealPlanStatus.Draft => status is MealPlanStatus.Active or MealPlanStatus.Cancelled,
            MealPlanStatus.Active => status is MealPlanStatus.Completed or MealPlanStatus.Cancelled,
            _ => false,
        };

        if (!allowed)
        {
            throw new DomainValidationException(new Dictionary<string, string[]>
            {
                ["status"] = [$"A plan cannot transition from {Status} to {status}."],
            });
        }

        Status = status;
        UpdatedAt = now;
    }

    public MealEntry AddEntry(
        DateOnly date,
        MealSlot slot,
        string title,
        Guid? recipeId,
        int servings,
        bool slotAlreadyExists,
        DateTimeOffset now)
    {
        EnsureEditable();
        ValidateEntryDate(date);
        if (slot is not MealSlot.Snack && slotAlreadyExists)
        {
            throw DuplicateSlot();
        }

        UpdatedAt = now;
        return MealEntry.Create(Id, date, slot, title, recipeId, servings, now);
    }

    public void RemoveEntry(MealEntry entry, DateTimeOffset now)
    {
        EnsureEditable();
        EnsureOwnedEntry(entry);
        if (entry.Status is not MealEntryStatus.Planned)
        {
            throw new DomainValidationException(new Dictionary<string, string[]>
            {
                ["status"] = ["Only planned meal entries can be removed."],
            });
        }
        UpdatedAt = now;
    }

    public void UpdateEntry(
        MealEntry entry,
        DateOnly date,
        MealSlot slot,
        string title,
        Guid? recipeId,
        int servings,
        bool slotAlreadyExists,
        DateTimeOffset now)
    {
        EnsureEditable();
        EnsureOwnedEntry(entry);
        ValidateEntryDate(date);
        if (slot is not MealSlot.Snack && slotAlreadyExists)
        {
            throw DuplicateSlot();
        }

        entry.Update(date, slot, title, recipeId, servings, now);
        UpdatedAt = now;
    }

    public void ChangeEntryStatus(MealEntry entry, MealEntryStatus status, DateTimeOffset now)
    {
        EnsureEditable();
        EnsureOwnedEntry(entry);
        entry.ChangeStatus(status, now);
        UpdatedAt = now;
    }

    private void ValidateEntryDate(DateOnly date)
    {
        if (date < StartDate || date > EndDate)
        {
            throw new DomainValidationException(new Dictionary<string, string[]>
            {
                ["date"] = ["Meal entry date must be within the plan's inclusive date range."],
            });
        }
    }

    private void EnsureOwnedEntry(MealEntry entry)
    {
        if (entry.MealPlanId != Id)
        {
            throw new KeyNotFoundException("Meal entry was not found.");
        }
    }

    private static DomainValidationException DuplicateSlot() =>
        new(new Dictionary<string, string[]>
        {
            ["slot"] = ["A meal entry already exists for this date and slot."],
        });

    private void EnsureEditable()
    {
        if (Status is MealPlanStatus.Completed or MealPlanStatus.Cancelled)
        {
            throw new DomainValidationException(new Dictionary<string, string[]>
            {
                ["status"] = [$"A {Status} plan cannot be changed."],
            });
        }
    }
}
