using Microsoft.Extensions.Options;
using UserService.Data.Storage;
using UserService.Domain.Services;

namespace UserService.UnitTests.Data;

public sealed class LocalProfilePictureStorageTests : IDisposable
{
    private readonly string rootPath = Path.Combine(Path.GetTempPath(), "UserService.UnitTests", Guid.NewGuid().ToString("N"));

    public LocalProfilePictureStorageTests()
    {
        Directory.CreateDirectory(rootPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task ReadAsync_returns_saved_bytes_for_valid_key()
    {
        var storage = CreateStorage();
        var bytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var key = await storage.SaveAsync(bytes, ProfilePictureFormat.Png, CancellationToken.None);

        var stream = await storage.ReadAsync(key, CancellationToken.None);

        Assert.NotNull(stream);
        await using var readableStream = stream!;

        using var copy = new MemoryStream();
        await readableStream.CopyToAsync(copy);
        Assert.Equal(bytes, copy.ToArray());
    }

    [Fact]
    public async Task ReadAsync_returns_null_for_missing_valid_key()
    {
        var storage = CreateStorage();
        var missingKey = $"{Guid.NewGuid():D}.jpg";

        var stream = await storage.ReadAsync(missingKey, CancellationToken.None);

        Assert.Null(stream);
    }

    [Theory]
    [InlineData("not-a-guid.jpg")]
    [InlineData("../x.png")]
    [InlineData("..\\x.png")]
    [InlineData("C:\\temp\\x.png")]
    [InlineData("/tmp/x.png")]
    public async Task ReadAsync_rejects_invalid_or_unsafe_keys(string key)
    {
        var storage = CreateStorage();

        await Assert.ThrowsAsync<ArgumentException>(() => storage.ReadAsync(key, CancellationToken.None));
    }

    private LocalProfilePictureStorage CreateStorage() =>
        new(Options.Create(new ProfilePictureStorageOptions
        {
            RootPath = rootPath,
        }));
}
