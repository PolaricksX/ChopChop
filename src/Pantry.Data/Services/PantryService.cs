using Pantry.Data.Repositories;
using Pantry.Domain.Entities;
using Pantry.Domain.Enums;
using Pantry.Domain.Services;

namespace Pantry.Data.Services;

public sealed class PantryService(PantryRepository repository) : IPantryService
{
    public async Task<IReadOnlyList<PantryItem>> ListAsync(
        string ownerId,
        string? search,
        int page,
        int pageSize,
        CancellationToken token) =>
        await repository.ListAsync(ownerId, search, (page - 1) * pageSize, pageSize, token);

    public Task<PantryItem?> GetAsync(string ownerId, Guid id, CancellationToken token) =>
        repository.GetAsync(ownerId, id, token);

    public Task<PantryItem> AddAsync(
        string ownerId,
        string name,
        decimal quantity,
        Unit unit,
        DateTimeOffset? expiresAt,
        CancellationToken token) =>
        repository.AddAsync(new PantryItem(Guid.NewGuid(), ownerId, name, PantryType.Custom,
            quantity, unit, expiresAt: expiresAt, customCategory: "General"), token);

    public async Task<bool> UpdateAsync(
        string ownerId,
        Guid id,
        decimal quantity,
        Unit unit,
        DateTimeOffset? expiresAt,
        byte[] version,
        CancellationToken token)
    {
        var item = await repository.GetAsync(ownerId, id, token);
        if (item is null)
            return false;

        item.Update(item.Name, item.Type, item.CustomCategory, item.Notes, quantity, unit, expiresAt);
        return await repository.UpdateAsync(item, version, token);
    }

    public async Task<bool> DeleteAsync(string ownerId, Guid id, CancellationToken token)
    {
        var item = await repository.GetAsync(ownerId, id, token);
        return item is not null && await repository.DeleteAsync(item, token);
    }
}
