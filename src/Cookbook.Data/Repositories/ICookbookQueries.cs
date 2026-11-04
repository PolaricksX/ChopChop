using Cookbook.Domain.Entities;

namespace Cookbook.Data.Repositories;

public interface ICookbookQueries
{
    Task<IReadOnlyList<Folder>> GetFoldersAsync(Guid ownerId, CancellationToken cancellationToken);
    Task<Folder?> GetFolderAsync(Guid ownerId, Guid folderId, CancellationToken cancellationToken);
    Task<Folder?> GetFolderForUpdateAsync(Guid ownerId, Guid folderId, CancellationToken cancellationToken);
}
