using SkiaSharp;

namespace UserService.Data.Images;

public sealed class SkiaSharpImageIntegrityChecker : IImageIntegrityChecker
{
    public Task<bool> IsValidImageAsync(byte[] imageBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (imageBytes.Length == 0)
        {
            return Task.FromResult(false);
        }

        using var bitmap = SKBitmap.Decode(imageBytes);
        return Task.FromResult(bitmap is not null && bitmap.Width > 0 && bitmap.Height > 0);
    }
}
