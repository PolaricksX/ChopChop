using MealPlanner.Api.Contracts;
using MealPlanner.Domain.Enums;

namespace MealPlanner.Api.Services;

public static class MealPlanRequestValidator
{
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 100;
    private const int MaximumPage = 1_000_000;

    public static IReadOnlyDictionary<string, string[]> Validate(CreateMealPlanRequest? request, string? idempotencyKey)
    {
        var errors = new Dictionary<string, string[]>();
        if (request is null)
        {
            errors["request"] = ["A request body is required."];
            return errors;
        }

        ValidatePlanFields(request.Name, request.StartDate, request.EndDate, errors);
        ValidateIdempotencyKey(idempotencyKey, errors);
        return errors;
    }

    public static IReadOnlyDictionary<string, string[]> Validate(UpdateMealPlanRequest? request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request is null)
        {
            errors["request"] = ["A request body is required."];
            return errors;
        }

        ValidatePlanFields(request.Name, request.StartDate, request.EndDate, errors);
        ValidateVersion(request.Version, errors);
        return errors;
    }

    public static IReadOnlyDictionary<string, string[]> Validate(ChangeMealPlanStatusRequest? request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request is null)
        {
            errors["request"] = ["A request body is required."];
            return errors;
        }

        if (!Enum.IsDefined(request.Status))
        {
            errors["status"] = ["Status is invalid."];
        }

        ValidateVersion(request.Version, errors);
        return errors;
    }

    public static IReadOnlyDictionary<string, string[]> Validate(
        CreateMealEntryRequest? request,
        string? idempotencyKey)
    {
        if (request is null)
        {
            return new Dictionary<string, string[]> { ["request"] = ["A request body is required."] };
        }

        var errors = ValidateMealEntry(
            request.Date,
            request.Slot,
            request.Title,
            request.Servings,
            request.Version).ToDictionary(error => error.Key, error => error.Value);
        ValidateIdempotencyKey(idempotencyKey, errors);

        return errors;
    }

    public static IReadOnlyDictionary<string, string[]> Validate(UpdateMealEntryRequest? request) =>
        request is null
            ? new Dictionary<string, string[]> { ["request"] = ["A request body is required."] }
            : ValidateMealEntry(request.Date, request.Slot, request.Title, request.Servings, request.Version);

    public static IReadOnlyDictionary<string, string[]> Validate(ChangeMealEntryStatusRequest? request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request is null)
        {
            errors["request"] = ["A request body is required."];
            return errors;
        }

        if (!Enum.IsDefined(request.Status))
        {
            errors["status"] = ["Status is invalid."];
        }

        ValidateVersion(request.Version, errors);
        return errors;
    }

    public static IReadOnlyDictionary<string, string[]> ValidatePage(int page, int pageSize)
    {
        var errors = new Dictionary<string, string[]>();
        if (page is < 1 or > MaximumPage)
        {
            errors["page"] = [$"Page must be between 1 and {MaximumPage}."];
        }

        if (pageSize is < 1 or > MaximumPageSize)
        {
            errors["pageSize"] = [$"Page size must be between 1 and {MaximumPageSize}."];
        }

        return errors;
    }

    public static bool TryDecodeVersion(string? version, out byte[] decoded) =>
        VersionToken.TryDecode(version, out decoded);

    private static IReadOnlyDictionary<string, string[]> ValidateMealEntry(
        DateOnly? date,
        MealSlot? slot,
        string? title,
        int? servings,
        string? version)
    {
        var errors = new Dictionary<string, string[]>();
        if (date is null || date == default)
        {
            errors["date"] = ["Date is required."];
        }

        if (slot is null || !Enum.IsDefined(slot.Value))
        {
            errors["slot"] = ["Slot is invalid."];
        }

        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200)
        {
            errors["title"] = ["Title is required and must be at most 200 characters."];
        }

        if (servings is null or < 1 or > 100)
        {
            errors["servings"] = ["Servings must be between 1 and 100."];
        }

        ValidateVersion(version, errors);
        return errors;
    }

    private static void ValidatePlanFields(
        string? name,
        DateOnly startDate,
        DateOnly endDate,
        Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120)
        {
            errors["name"] = ["Name is required and must be at most 120 characters."];
        }

        if (startDate == default || endDate == default)
        {
            errors["date"] = ["Start and end dates are required."];
        }

        if (endDate < startDate)
        {
            errors["endDate"] = ["End date must be on or after start date."];
        }
        else if (endDate.DayNumber - startDate.DayNumber + 1 > 366)
        {
            errors["endDate"] = ["A meal plan cannot exceed 366 calendar days."];
        }
    }

    private static void ValidateVersion(string? version, Dictionary<string, string[]> errors)
    {
        if (!VersionToken.TryDecode(version, out _))
        {
            errors["version"] = ["A valid version token from the last response is required."];
        }
    }

    private static void ValidateIdempotencyKey(string? key, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 128 || key.Any(char.IsControl))
        {
            errors["Idempotency-Key"] = ["A non-empty Idempotency-Key of at most 128 characters is required."];
        }
    }
}

public static class VersionToken
{
    public static string Encode(byte[] version) => Convert.ToBase64String(version);

    public static bool TryDecode(string? version, out byte[] decoded)
    {
        decoded = [];
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        try
        {
            decoded = Convert.FromBase64String(version);
            return decoded.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
