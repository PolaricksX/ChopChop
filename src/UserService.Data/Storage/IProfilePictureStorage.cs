using UserService.Domain.Services;

namespace UserService.Data.Storage;

public interface IProfilePictureStorage
{
    Task<string> SaveAsync(byte[] bytes, ProfilePictureFormat format, CancellationToken cancellationToken);

    Task<Stream?> ReadAsync(string key, CancellationToken cancellationToken);

    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
