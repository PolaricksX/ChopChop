using MealPlanner.Domain.Enums;

namespace MealPlanner.Api.Contracts;

/// <summary>Request to create a caller-owned plan using inclusive calendar dates.</summary>
public sealed record CreateMealPlanRequest(string? Name, DateOnly StartDate, DateOnly EndDate);

/// <summary>Request to replace editable plan details using the last returned version token.</summary>
public sealed record UpdateMealPlanRequest(string? Name, DateOnly StartDate, DateOnly EndDate, string? Version);

/// <summary>Request to change a plan lifecycle status.</summary>
public sealed record ChangeMealPlanStatusRequest(MealPlanStatus Status, string? Version);

/// <summary>Meal plan data returned by the API; version is required for subsequent writes.</summary>
public sealed record MealPlanResponse(
    Guid Id,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    MealPlanStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string Version);

/// <summary>A page of caller-owned meal plans.</summary>
public sealed record MealPlanPageResponse(
    IReadOnlyList<MealPlanResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

/// <summary>Request to add a meal entry to a plan; the plan version prevents lost updates.</summary>
public sealed record CreateMealEntryRequest(
    DateOnly Date,
    MealSlot Slot,
    string? Title,
    Guid? RecipeId,
    int Servings,
    string? Version);

/// <summary>Request to replace a planned meal entry.</summary>
public sealed record UpdateMealEntryRequest(
    DateOnly Date,
    MealSlot Slot,
    string? Title,
    Guid? RecipeId,
    int Servings,
    string? Version);

/// <summary>Request to record progress for a meal entry.</summary>
public sealed record ChangeMealEntryStatusRequest(MealEntryStatus Status, string? Version);

/// <summary>A meal entry, including a stable recipe ID and a title snapshot.</summary>
public sealed record MealEntryResponse(
    Guid Id,
    Guid MealPlanId,
    DateOnly Date,
    MealSlot Slot,
    string Title,
    Guid? RecipeId,
    int Servings,
    MealEntryStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string Version);

/// <summary>A page of meal entries belonging to one caller-owned plan.</summary>
public sealed record MealEntryPageResponse(
    IReadOnlyList<MealEntryResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
