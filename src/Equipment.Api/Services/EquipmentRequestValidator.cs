using Equipment.Api.Contracts;
using Equipment.Data.Repositories;
using Equipment.Domain.Entities;
using Equipment.Domain.Enums;

namespace Equipment.Api.Services;

public static class EquipmentRequestValidator
{
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 100;
    public const int MaximumPage = 1_000_000;

    public static IReadOnlyDictionary<string, string[]> ValidateCreate(
        EquipmentUpsertRequest? request,
        string? idempotencyKey)
    {
        var errors = ValidateDetails(request);
        ValidateIdempotencyKey(idempotencyKey, errors);
        return errors;
    }

    public static IReadOnlyDictionary<string, string[]> ValidateUpdate(
        EquipmentUpsertRequest? request,
        string? ifMatch)
    {
        var errors = ValidateDetails(request);
        ValidateVersion(ifMatch, errors);
        return errors;
    }

    public static IReadOnlyDictionary<string, string[]> ValidateAvailability(
        EquipmentAvailabilityRequest? request,
        string? ifMatch)
    {
        var errors = new Dictionary<string, string[]>();
        if (request?.IsAvailable is null)
        {
            errors["isAvailable"] = ["Availability is required."];
        }

        ValidateVersion(ifMatch, errors);
        return errors;
    }

    public static IReadOnlyDictionary<string, string[]> ValidateDelete(string? ifMatch)
    {
        var errors = new Dictionary<string, string[]>();
        ValidateVersion(ifMatch, errors);
        return errors;
    }

    public static IReadOnlyDictionary<string, string[]> ValidateList(
        int page,
        int pageSize,
        string? search,
        EquipmentType? type,
        EquipmentCondition? condition,
        EquipmentAvailability? availability)
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

        if (search?.Trim().Length > EquipmentQuery.MaxSearchLength)
        {
            errors["search"] = [$"Search must be at most {EquipmentQuery.MaxSearchLength} characters."];
        }

        if (type.HasValue && !Enum.IsDefined(type.Value))
        {
            errors["type"] = ["Equipment type is invalid."];
        }

        if (condition.HasValue && !Enum.IsDefined(condition.Value))
        {
            errors["condition"] = ["Equipment condition is invalid."];
        }

        if (availability.HasValue && !Enum.IsDefined(availability.Value))
        {
            errors["availability"] = ["Equipment availability is invalid."];
        }

        return errors;
    }

    public static bool TryDecodeVersion(string? version, out byte[] decoded) =>
        VersionToken.TryDecode(version, out decoded);

    private static Dictionary<string, string[]> ValidateDetails(EquipmentUpsertRequest? request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request is null)
        {
            errors["request"] = ["A request body is required."];
            return errors;
        }

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > EquipmentItem.MaxNameLength)
        {
            errors["name"] = [$"Name is required and must be at most {EquipmentItem.MaxNameLength} characters."];
        }

        if (request.Type is null || !Enum.IsDefined(request.Type.Value))
        {
            errors["type"] = ["A valid equipment type is required."];
        }

        if (request.Quantity is null or < 1 or > EquipmentItem.MaxQuantity)
        {
            errors["quantity"] = [$"Quantity must be between 1 and {EquipmentItem.MaxQuantity}."];
        }

        if (request.Condition is null || !Enum.IsDefined(request.Condition.Value))
        {
            errors["condition"] = ["A valid equipment condition is required."];
        }

        ValidateOptional(request.Brand, EquipmentItem.MaxBrandLength, "brand", errors);
        ValidateOptional(request.Size, EquipmentItem.MaxSizeLength, "size", errors);
        ValidateOptional(request.Notes, EquipmentItem.MaxNotesLength, "notes", errors);
        ValidateOptional(request.Location, EquipmentItem.MaxLocationLength, "location", errors);
        return errors;
    }

    private static void ValidateOptional(
        string? value,
        int maximumLength,
        string field,
        Dictionary<string, string[]> errors)
    {
        if (value is not null && value.Trim().Length > maximumLength)
        {
            errors[field] = [$"{field} must be at most {maximumLength} characters."];
        }
    }

    private static void ValidateVersion(string? version, Dictionary<string, string[]> errors)
    {
        if (!VersionToken.TryDecode(version, out _))
        {
            errors["If-Match"] = ["A valid ETag from the latest equipment response is required."];
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
    public const int RowVersionLength = 8;

    public static string Encode(byte[] version) => Convert.ToBase64String(version);

    public static bool TryDecode(string? version, out byte[] decoded)
    {
        decoded = [];
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        var token = version.Trim();
        if (token.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (token.Length >= 2 && token[0] == '"' && token[^1] == '"')
        {
            token = token[1..^1];
        }

        try
        {
            decoded = Convert.FromBase64String(token);
            return decoded.Length == RowVersionLength;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
