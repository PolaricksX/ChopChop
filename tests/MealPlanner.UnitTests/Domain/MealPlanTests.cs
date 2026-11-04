using MealPlanner.Domain.Entities;
using MealPlanner.Domain.Enums;
using MealPlanner.Domain.Exceptions;

namespace MealPlanner.UnitTests.Domain;

public sealed class MealPlanTests
{
    private static readonly DateOnly Start = new(2026, 11, 1);
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_rejects_reversed_and_oversized_ranges()
    {
        Assert.Throws<DomainValidationException>(() =>
            MealPlan.Create("owner", "Plan", Start.AddDays(1), Start, Now));
        Assert.Throws<DomainValidationException>(() =>
            MealPlan.Create("owner", "Plan", Start, Start.AddDays(366), Now));
    }

    [Fact]
    public void AddEntry_requires_date_inside_inclusive_plan_range()
    {
        var plan = CreatePlan();

        Assert.Throws<DomainValidationException>(() =>
            plan.AddEntry(Start.AddDays(-1), MealSlot.Breakfast, "Oats", null, 1, false, Now));
        Assert.Throws<DomainValidationException>(() =>
            plan.AddEntry(Start.AddDays(7), MealSlot.Breakfast, "Oats", null, 1, false, Now));
        Assert.Equal(Start, plan.AddEntry(Start, MealSlot.Breakfast, "Oats", null, 1, false, Now).Date);
    }

    [Fact]
    public void AddEntry_rejects_duplicate_meals_but_allows_other_slots_and_multiple_snacks()
    {
        var plan = CreatePlan();
        plan.AddEntry(Start, MealSlot.Breakfast, "Oats", null, 1, false, Now);

        Assert.Throws<DomainValidationException>(() =>
            plan.AddEntry(Start, MealSlot.Breakfast, "Eggs", null, 1, true, Now));
        Assert.Equal(MealSlot.Lunch,
            plan.AddEntry(Start, MealSlot.Lunch, "Soup", null, 1, false, Now).Slot);
        Assert.Equal(2, new[]
        {
            plan.AddEntry(Start, MealSlot.Snack, "Fruit", null, 1, false, Now),
            plan.AddEntry(Start, MealSlot.Snack, "Yogurt", null, 1, false, Now),
        }.Length);
    }

    [Fact]
    public void Entry_status_transitions_are_terminal_after_completion_or_skip()
    {
        var plan = CreatePlan();
        var entry = plan.AddEntry(Start, MealSlot.Breakfast, "Oats", null, 1, false, Now);

        plan.ChangeEntryStatus(entry, MealEntryStatus.InProgress, Now);
        plan.ChangeEntryStatus(entry, MealEntryStatus.Completed, Now);

        Assert.Throws<DomainValidationException>(() =>
            plan.ChangeEntryStatus(entry, MealEntryStatus.Skipped, Now));
    }

    [Fact]
    public void Plan_status_transitions_prevent_mutations_after_completion()
    {
        var plan = CreatePlan();
        plan.ChangeStatus(MealPlanStatus.Active, Now);
        plan.ChangeStatus(MealPlanStatus.Completed, Now);

        Assert.Throws<DomainValidationException>(() =>
            plan.AddEntry(Start, MealSlot.Breakfast, "Oats", null, 1, false, Now));
        Assert.Throws<DomainValidationException>(() =>
            plan.ChangeStatus(MealPlanStatus.Active, Now));
    }

    [Fact]
    public void Update_rejects_range_that_excludes_existing_entry()
    {
        var plan = CreatePlan();
        Assert.Throws<DomainValidationException>(() =>
            plan.Update("Plan", Start.AddDays(1), Start.AddDays(6), false, Now));
    }

    [Fact]
    public void Entries_require_bounded_title_and_servings()
    {
        var plan = CreatePlan();

        Assert.Throws<DomainValidationException>(() =>
            plan.AddEntry(Start, MealSlot.Breakfast, " ", null, 1, false, Now));
        Assert.Throws<DomainValidationException>(() =>
            plan.AddEntry(Start, MealSlot.Breakfast, "Oats", null, 0, false, Now));
    }

    private static MealPlan CreatePlan() =>
        MealPlan.Create("owner", "Week", Start, Start.AddDays(6), Now);
}
