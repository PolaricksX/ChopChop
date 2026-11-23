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

        try
        {
            using var data = SKData.CreateCopy(imageBytes);
            using var codec = SKCodec.Create(data);
            if (codec is null || codec.Info.Width <= 0 || codec.Info.Height <= 0)
            {
                return Task.FromResult(false);
            }

            using var bitmap = SKBitmap.Decode(codec);
            return Task.FromResult(bitmap is not null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Task.FromResult(false);
        }
    }
}
