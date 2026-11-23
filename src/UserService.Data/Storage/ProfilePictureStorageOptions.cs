using System.ComponentModel.DataAnnotations;

namespace UserService.Data.Storage;

public sealed class ProfilePictureStorageOptions
{
    public const string SectionName = "ProfilePictures";

    [Required]
    public string RootPath { get; set; } = string.Empty;

    public static bool IsValid(ProfilePictureStorageOptions options) =>
        !string.IsNullOrWhiteSpace(options.RootPath) && Path.IsPathFullyQualified(options.RootPath);
}
