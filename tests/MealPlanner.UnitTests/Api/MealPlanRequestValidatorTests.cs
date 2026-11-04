using MealPlanner.Api.Contracts;
using MealPlanner.Api.Services;
using MealPlanner.Domain.Enums;

namespace MealPlanner.UnitTests.Api;

public sealed class MealPlanRequestValidatorTests
{
    [Fact]
    public void Create_plan_requires_idempotency_key_and_valid_dates()
    {
        var errors = MealPlanRequestValidator.Validate(
            new CreateMealPlanRequest("Week", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 2)),
            null);

        Assert.Contains("Idempotency-Key", errors.Keys);
    }

    [Fact]
    public void Entry_request_rejects_undefined_slot_and_invalid_version()
    {
        var errors = MealPlanRequestValidator.Validate(
            new CreateMealEntryRequest(
                new DateOnly(2026, 11, 1),
                (MealSlot)999,
                "Oats",
                null,
                1,
                "not-base64"),
            "key");

        Assert.Contains("slot", errors.Keys);
        Assert.Contains("version", errors.Keys);
    }

    [Fact]
    public void Pagination_enforces_documented_bounds()
    {
        Assert.Empty(MealPlanRequestValidator.ValidatePage(1, 100));
        Assert.Contains("pageSize", MealPlanRequestValidator.ValidatePage(1, 101).Keys);
        Assert.Contains("page", MealPlanRequestValidator.ValidatePage(0, 20).Keys);
    }
}
