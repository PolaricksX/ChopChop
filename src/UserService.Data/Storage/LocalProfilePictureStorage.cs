using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using UserService.Domain.Services;

namespace UserService.Data.Storage;

public sealed partial class LocalProfilePictureStorage(IOptions<ProfilePictureStorageOptions> options) : IProfilePictureStorage
{
    private static readonly string RootPathSeparator = Path.DirectorySeparatorChar.ToString();
    private readonly string rootPath = NormalizeRoot(options.Value.RootPath);

    public async Task<string> SaveAsync(byte[] bytes, ProfilePictureFormat format, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var extension = format switch
        {
            ProfilePictureFormat.Jpeg => ".jpg",
            ProfilePictureFormat.Png => ".png",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
        };

        var key = $"{Guid.NewGuid():D}{extension}";
        var path = ResolvePath(key);
        await File.WriteAllBytesAsync(path, bytes, cancellationToken);
        return key;
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var path = ResolveValidatedPath(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public Task<Stream?> ReadAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var path = ResolveValidatedPath(key);
        if (!File.Exists(path))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous);

        return Task.FromResult<Stream?>(stream);
    }

    private static string NormalizeRoot(string configuredRootPath)
    {
        if (string.IsNullOrWhiteSpace(configuredRootPath) || !Path.IsPathFullyQualified(configuredRootPath))
        {
            throw new OptionsValidationException(
                ProfilePictureStorageOptions.SectionName,
                typeof(ProfilePictureStorageOptions),
                ["ProfilePictures:RootPath is required and must be an absolute path."]);
        }

        var normalized = Path.GetFullPath(configuredRootPath);
        Directory.CreateDirectory(normalized);
        return normalized;
    }

    private string ResolvePath(string key)
    {
        var fullPath = Path.GetFullPath(Path.Combine(rootPath, key));
        var normalizedRootWithSeparator = rootPath.EndsWith(RootPathSeparator, StringComparison.Ordinal)
            ? rootPath
            : rootPath + RootPathSeparator;

        if (!fullPath.StartsWith(normalizedRootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Profile picture key resolves outside the configured root path.", nameof(key));
        }

        return fullPath;
    }

    private string ResolveValidatedPath(string key)
    {
        if (!KeyPattern().IsMatch(key))
        {
            throw new ArgumentException("Profile picture key must be a GUID file name ending in .jpg or .png.", nameof(key));
        }

        return ResolvePath(key);
    }

    [GeneratedRegex(@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\.(jpg|png)$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();
}
