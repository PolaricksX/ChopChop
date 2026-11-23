namespace UserService.Data.Images;

public interface IImageIntegrityChecker
{
    Task<bool> IsValidImageAsync(byte[] imageBytes, CancellationToken cancellationToken);
}
