using Cookbook.Domain.Exceptions;

namespace Cookbook.Domain.Services;

public static class FolderRules
{
    public const int MaximumNameLength = 100;

    public static string NormalizeName(string? name)
    {
        var normalizedName = name?.Trim() ?? string.Empty;
        if (normalizedName.Length == 0 || normalizedName.Length > MaximumNameLength)
            throw new CookbookValidationException("A folder name of 1 to 100 characters is required.");

        return normalizedName;
    }

    public static void ValidateOwner(Guid ownerId)
    {
        if (ownerId == Guid.Empty)
            throw new CookbookValidationException("An owner identifier is required.");
    }

    public static DateTimeOffset NormalizeTimestamp(DateTimeOffset timestamp, string parameterName)
    {
        if (timestamp == default)
            throw new CookbookValidationException($"A {parameterName} timestamp is required.");

        return timestamp.ToUniversalTime();
    }
}
