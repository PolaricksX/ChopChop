namespace UserService.Domain.Services;

public enum ProfilePictureValidationFailure
{
    Empty,
    TooLarge,
    UnsupportedFormat,
    ContentTypeMismatch,
}

public enum ProfilePictureFormat
{
    Jpeg,
    Png,
}

public readonly record struct ProfilePictureValidationResult(
    bool IsValid,
    ProfilePictureValidationFailure? Failure,
    ProfilePictureFormat? DetectedFormat)
{
    public static ProfilePictureValidationResult Success(ProfilePictureFormat format) => new(true, null, format);

    public static ProfilePictureValidationResult Invalid(ProfilePictureValidationFailure failure) => new(false, failure, null);
}

/// <summary>Pure domain rules for profile-picture upload validation.</summary>
public static class ProfilePictureRules
{
    public const int MaxFileSizeBytes = 1_048_576;
    public const string JpegContentType = "image/jpeg";
    public const string PngContentType = "image/png";

    public static ProfilePictureValidationResult Validate(byte[]? fileBytes, string? declaredContentType)
    {
        if (fileBytes is null || fileBytes.Length == 0)
        {
            return ProfilePictureValidationResult.Invalid(ProfilePictureValidationFailure.Empty);
        }

        if (fileBytes.Length > MaxFileSizeBytes)
        {
            return ProfilePictureValidationResult.Invalid(ProfilePictureValidationFailure.TooLarge);
        }

        var detectedFormat = DetectFormat(fileBytes);
        if (detectedFormat is null)
        {
            return ProfilePictureValidationResult.Invalid(ProfilePictureValidationFailure.UnsupportedFormat);
        }

        var expectedContentType = GetContentType(detectedFormat.Value);
        var normalizedDeclaredContentType = NormalizeContentType(declaredContentType);
        if (!string.Equals(expectedContentType, normalizedDeclaredContentType, StringComparison.Ordinal))
        {
            return ProfilePictureValidationResult.Invalid(ProfilePictureValidationFailure.ContentTypeMismatch);
        }

        return ProfilePictureValidationResult.Success(detectedFormat.Value);
    }

    public static ProfilePictureFormat? DetectFormat(byte[]? fileBytes)
    {
        if (fileBytes is null)
        {
            return null;
        }

        if (IsJpeg(fileBytes))
        {
            return ProfilePictureFormat.Jpeg;
        }

        if (IsPng(fileBytes))
        {
            return ProfilePictureFormat.Png;
        }

        return null;
    }

    public static bool TryNormalizeAllowedContentType(string? contentType, out string normalizedContentType)
    {
        normalizedContentType = NormalizeContentType(contentType) ?? string.Empty;
        return normalizedContentType is JpegContentType or PngContentType;
    }

    public static string GetContentType(ProfilePictureFormat format) => format switch
    {
        ProfilePictureFormat.Jpeg => JpegContentType,
        ProfilePictureFormat.Png => PngContentType,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    private static string? NormalizeContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return null;
        }

        var separatorIndex = contentType.IndexOf(';');
        var mediaType = separatorIndex >= 0 ? contentType[..separatorIndex] : contentType;
        return mediaType.Trim().ToLowerInvariant();
    }

    private static bool IsJpeg(IReadOnlyList<byte> fileBytes) =>
        fileBytes.Count >= 3 &&
        fileBytes[0] == 0xFF &&
        fileBytes[1] == 0xD8 &&
        fileBytes[2] == 0xFF;

    private static bool IsPng(IReadOnlyList<byte> fileBytes) =>
        fileBytes.Count >= 8 &&
        fileBytes[0] == 0x89 &&
        fileBytes[1] == 0x50 &&
        fileBytes[2] == 0x4E &&
        fileBytes[3] == 0x47 &&
        fileBytes[4] == 0x0D &&
        fileBytes[5] == 0x0A &&
        fileBytes[6] == 0x1A &&
        fileBytes[7] == 0x0A;
}
