using Cookbook.Data.Context;
using Cookbook.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cookbook.Data.Repositories;

internal sealed class CookbookQueries(CookbookDbContext context) : ICookbookQueries
{
    public async Task<IReadOnlyList<Folder>> GetFoldersAsync(
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        return await context.Folders
            .AsNoTracking()
            .Where(folder => folder.OwnerId == ownerId)
            .OrderBy(folder => folder.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<Folder?> GetFolderAsync(
        Guid ownerId,
        Guid folderId,
        CancellationToken cancellationToken) =>
        context.Folders
            .AsNoTracking()
            .AsSplitQuery()
            .Include(folder => folder.SavedRecipes)
            .SingleOrDefaultAsync(
                folder => folder.Id == folderId && folder.OwnerId == ownerId,
                cancellationToken);

    public Task<Folder?> GetFolderForUpdateAsync(
        Guid ownerId,
        Guid folderId,
        CancellationToken cancellationToken) =>
        context.Folders
            .AsTracking()
            .AsSplitQuery()
            .Include(folder => folder.SavedRecipes)
            .SingleOrDefaultAsync(
                folder => folder.Id == folderId && folder.OwnerId == ownerId,
                cancellationToken);
}
