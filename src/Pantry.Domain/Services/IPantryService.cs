using Pantry.Domain.Entities;
using Pantry.Domain.Enums;

namespace Pantry.Domain.Services;

public interface IPantryService
{
    Task<IReadOnlyList<PantryItem>> ListAsync(string ownerId, string? search, int page, int pageSize, CancellationToken token);
    Task<PantryItem?> GetAsync(string ownerId, Guid id, CancellationToken token);
    Task<PantryItem> AddAsync(string ownerId, string name, decimal quantity, Unit unit, DateTimeOffset? expiresAt, CancellationToken token);
    Task<bool> UpdateAsync(string ownerId, Guid id, decimal quantity, Unit unit, DateTimeOffset? expiresAt, byte[] version, CancellationToken token);
    Task<bool> DeleteAsync(string ownerId, Guid id, CancellationToken token);
}
