using RecipeManagement.Domain.Enums;

namespace RecipeManagement.Api.Contracts;

public static class RecipeRequestValidator
{
    public const int MaxSearchTextLength = 200;
    public const int MaxSearchItems = 50;
    public const int MaxSearchItemLength = 200;
    public const int MaxSearchTotalMinutes = 1_440;
    public const int MaxSearchServings = 100;

    public static void Validate(RecipeContentRequest request)
    {
        var errors = new List<string>();
        if (request is null) errors.Add("Request body is required.");
        else
        {
            if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200) errors.Add("Title is required and must be at most 200 characters.");
            if (request.Description?.Length > 5_000) errors.Add("Description must be at most 5000 characters.");
            if (request.Servings <= 0) errors.Add("Servings must be positive.");
            if (request.Ingredients is null || request.Ingredients.Count is < 1 or > 100) errors.Add("Ingredients must contain 1 to 100 items.");
            else foreach (var item in request.Ingredients) { if (string.IsNullOrWhiteSpace(item.Name) || item.Name.Length > 200) errors.Add("Ingredient names must be 1 to 200 characters."); if (item.Quantity <= 0) errors.Add("Ingredient quantities must be positive."); if (string.IsNullOrWhiteSpace(item.Unit) || item.Unit.Length > 50) errors.Add("Ingredient units are required and must be at most 50 characters."); }
            if (request.Steps is null || request.Steps.Count is < 1 or > 100) errors.Add("Steps must contain 1 to 100 items.");
            else if (request.Steps.Any(x => x.Order < 1 || string.IsNullOrWhiteSpace(x.Content) || x.Content.Length > 2_000)) errors.Add("Steps must have positive order and content of at most 2000 characters.");
            if (request.AllergenStatus == AllergenStatus.Unknown && request.Allergens?.Count > 0) errors.Add("Unknown allergen status cannot include allergens.");
            if (request.Tags?.Count > 50 || request.DietaryLabels?.Count > 50 || request.Equipment?.Count > 50) errors.Add("Tag, dietary label, and equipment limits were exceeded.");
            if (request.PrepMinutes < 0 || request.CookMinutes < 0 || request.TotalMinutes < 0) errors.Add("Recipe times cannot be negative.");
        }
        if (errors.Count > 0) throw new RequestValidationException(errors);
    }
    public static void Validate(UpdateRecipeRequest request) { if (request is null || request.ExpectedVersion < 1) throw new RequestValidationException(["A positive expected version is required."]); Validate(request.Content); }
    public static void ValidatePage(int page, int pageSize) { if (page is < 1 or > 10_000 || pageSize is < 1 or > 100) throw new RequestValidationException(["Page must be 1..10000 and pageSize must be 1..100."]); }

    public static int ParseSearchPage(string? value, int defaultValue, string parameterName)
        => ParseSearchInt(value, defaultValue, 1, 10_000, parameterName);

    public static int? ParseOptionalSearchInt(string? value, int minimum, int maximum, string parameterName)
        => string.IsNullOrWhiteSpace(value) ? null : ParseSearchInt(value, null, minimum, maximum, parameterName);

    public static IReadOnlyList<string>? ParseSearchList(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var items = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (items.Length is 0 or > MaxSearchItems || items.Any(item => item.Length > MaxSearchItemLength || item.Length == 0))
            throw new RequestValidationException([$"{parameterName} must contain at most {MaxSearchItems} items of at most {MaxSearchItemLength} characters each."]);

        return items;
    }

    public static IReadOnlyList<T>? ParseSearchEnumList<T>(string? value, string parameterName) where T : struct, Enum
    {
        var values = ParseSearchList(value, parameterName);
        if (values is null) return null;

        var parsed = new T[values.Count];
        for (var index = 0; index < values.Count; index++)
        {
            if (!Enum.TryParse<T>(values[index], ignoreCase: true, out var item) || !Enum.IsDefined(item))
                throw new RequestValidationException([$"{parameterName} contains an invalid value: '{values[index]}'."]);
            parsed[index] = item;
        }

        return parsed;
    }

    public static string? ParseSearchText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Trim();
        if (text.Length > MaxSearchTextLength)
            throw new RequestValidationException([$"text must be at most {MaxSearchTextLength} characters."]);
        return text;
    }

    public static string ParseSearchSort(string? value)
    {
        var sort = string.IsNullOrWhiteSpace(value) ? "updated" : value.Trim().ToLowerInvariant();
        if (sort is not ("title" or "totalminutes" or "servings" or "publishedat" or "updated"))
            throw new RequestValidationException(["sort must be one of: title, totalminutes, servings, publishedat, updated."]);
        return sort;
    }

    public static bool ParseSearchDirection(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "desc", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(value, "asc", StringComparison.OrdinalIgnoreCase)) return false;
        throw new RequestValidationException(["direction must be either 'asc' or 'desc'."]);
    }

    private static int ParseSearchInt(string? value, int defaultValue, int minimum, int maximum, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) return defaultValue;
        if (!int.TryParse(value, out var parsed) || parsed < minimum || parsed > maximum)
            throw new RequestValidationException([$"{parameterName} must be an integer from {minimum} to {maximum}."]);
        return parsed;
    }

    private static int? ParseSearchInt(string? value, int? defaultValue, int minimum, int maximum, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) return defaultValue;
        if (!int.TryParse(value, out var parsed) || parsed < minimum || parsed > maximum)
            throw new RequestValidationException([$"{parameterName} must be an integer from {minimum} to {maximum}."]);
        return parsed;
    }
}

public sealed class RequestValidationException(IReadOnlyList<string> errors) : Exception("The request is invalid.") { public IReadOnlyList<string> Errors { get; } = errors; }
