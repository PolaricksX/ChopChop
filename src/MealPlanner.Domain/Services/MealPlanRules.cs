namespace MealPlanner.Domain.Services;

public static class MealPlanRules
{
    public const int OwnerMaxLength = 256;
    public const int NameMaxLength = 120;
    public const int TitleMaxLength = 200;
    public const int MaxPlanDays = 366;
    public const int MaxServings = 100;

    public static IReadOnlyDictionary<string, string[]> ValidatePlan(string? ownerId, string? name, DateOnly startDate, DateOnly endDate)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(ownerId) || ownerId.Length > OwnerMaxLength)
        {
            errors["ownerId"] = ["Owner is required and must be at most 256 characters."];
        }

        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > NameMaxLength)
        {
            errors["name"] = ["Name is required and must be at most 120 characters."];
        }

        if (endDate < startDate)
        {
            errors["endDate"] = ["End date must be on or after start date."];
        }
        else if (endDate.DayNumber - startDate.DayNumber + 1 > MaxPlanDays)
        {
            errors["endDate"] = ["A meal plan cannot exceed 366 calendar days."];
        }

        return errors;
    }

    public static IReadOnlyDictionary<string, string[]> ValidateEntry(string? title, int servings)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > TitleMaxLength)
        {
            errors["title"] = ["Title is required and must be at most 200 characters."];
        }

        if (servings is < 1 or > MaxServings)
        {
            errors["servings"] = ["Servings must be between 1 and 100."];
        }

        return errors;
    }
}
