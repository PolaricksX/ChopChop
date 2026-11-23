using UserService.Domain.Services;

namespace UserService.UnitTests.Domain;

public class ProfilePictureRulesTests
{
    private static readonly byte[] ValidJpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x01];
    private static readonly byte[] ValidPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];

    [Fact]
    public void Validate_rejects_empty_file()
    {
        var result = ProfilePictureRules.Validate([], ProfilePictureRules.JpegContentType);

        Assert.False(result.IsValid);
        Assert.Equal(ProfilePictureValidationFailure.Empty, result.Failure);
    }

    [Fact]
    public void Validate_rejects_file_larger_than_1_mb()
    {
        var oversized = new byte[ProfilePictureRules.MaxFileSizeBytes + 1];
        oversized[0] = 0xFF;
        oversized[1] = 0xD8;
        oversized[2] = 0xFF;

        var result = ProfilePictureRules.Validate(oversized, ProfilePictureRules.JpegContentType);

        Assert.False(result.IsValid);
        Assert.Equal(ProfilePictureValidationFailure.TooLarge, result.Failure);
    }

    [Fact]
    public void Validate_rejects_unsupported_format_by_signature()
    {
        var result = ProfilePictureRules.Validate([0x47, 0x49, 0x46], "image/gif");

        Assert.False(result.IsValid);
        Assert.Equal(ProfilePictureValidationFailure.UnsupportedFormat, result.Failure);
    }

    [Fact]
    public void Validate_rejects_declared_content_type_mismatch()
    {
        var result = ProfilePictureRules.Validate(ValidJpeg, ProfilePictureRules.PngContentType);

        Assert.False(result.IsValid);
        Assert.Equal(ProfilePictureValidationFailure.ContentTypeMismatch, result.Failure);
    }

    [Theory]
    [MemberData(nameof(ValidCases))]
    public void Validate_accepts_allowed_format_when_declared_content_type_matches(
        byte[] fileBytes,
        string declaredContentType,
        ProfilePictureFormat expectedFormat)
    {
        var result = ProfilePictureRules.Validate(fileBytes, declaredContentType);

        Assert.True(result.IsValid);
        Assert.Null(result.Failure);
        Assert.Equal(expectedFormat, result.DetectedFormat);
    }

    [Theory]
    [InlineData(" image/jpeg ", ProfilePictureRules.JpegContentType)]
    [InlineData("IMAGE/PNG", ProfilePictureRules.PngContentType)]
    [InlineData("image/png; charset=binary", ProfilePictureRules.PngContentType)]
    public void TryNormalizeAllowedContentType_accepts_and_normalizes(string contentType, string expected)
    {
        var isAllowed = ProfilePictureRules.TryNormalizeAllowedContentType(contentType, out var normalized);

        Assert.True(isAllowed);
        Assert.Equal(expected, normalized);
    }

    public static TheoryData<byte[], string, ProfilePictureFormat> ValidCases() =>
        new()
        {
            { ValidJpeg, ProfilePictureRules.JpegContentType, ProfilePictureFormat.Jpeg },
            { ValidPng, ProfilePictureRules.PngContentType, ProfilePictureFormat.Png },
        };
}
