using MealPlanner.Domain.Enums;
using MealPlanner.Domain.Exceptions;
using MealPlanner.Domain.Services;

namespace MealPlanner.Domain.Entities;

public sealed class MealEntry
{
    private MealEntry()
    {
    }

    public Guid Id { get; private set; }

    public Guid MealPlanId { get; private set; }

    public DateOnly Date { get; private set; }

    public MealSlot Slot { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public Guid? RecipeId { get; private set; }

    public int Servings { get; private set; }

    public MealEntryStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    internal static MealEntry Create(
        Guid mealPlanId,
        DateOnly date,
        MealSlot slot,
        string title,
        Guid? recipeId,
        int servings,
        DateTimeOffset now)
    {
        Validate(title, servings);
        return new MealEntry
        {
            Id = Guid.NewGuid(),
            MealPlanId = mealPlanId,
            Date = date,
            Slot = slot,
            Title = title.Trim(),
            RecipeId = recipeId,
            Servings = servings,
            Status = MealEntryStatus.Planned,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    internal void Update(DateOnly date, MealSlot slot, string title, Guid? recipeId, int servings, DateTimeOffset now)
    {
        if (Status is not MealEntryStatus.Planned)
        {
            throw new DomainValidationException(new Dictionary<string, string[]>
            {
                ["status"] = ["Only planned meal entries can be edited."],
            });
        }

        Validate(title, servings);
        Date = date;
        Slot = slot;
        Title = title.Trim();
        RecipeId = recipeId;
        Servings = servings;
        UpdatedAt = now;
    }

    internal void ChangeStatus(MealEntryStatus status, DateTimeOffset now)
    {
        var allowed = Status switch
        {
            MealEntryStatus.Planned => status is MealEntryStatus.InProgress or MealEntryStatus.Completed or MealEntryStatus.Skipped,
            MealEntryStatus.InProgress => status is MealEntryStatus.Completed or MealEntryStatus.Skipped,
            _ => false,
        };

        if (!allowed)
        {
            throw new DomainValidationException(new Dictionary<string, string[]>
            {
                ["status"] = [$"A meal entry cannot transition from {Status} to {status}."],
            });
        }

        Status = status;
        UpdatedAt = now;
    }

    private static void Validate(string title, int servings)
    {
        var errors = MealPlanRules.ValidateEntry(title, servings);
        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }
    }
}
