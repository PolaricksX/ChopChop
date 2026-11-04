using Pantry.Api.Contracts;
using Pantry.Data.Repositories;
using Pantry.Domain.Entities;
using Pantry.Domain.Enums;

namespace Pantry.Api.Infrastructure;

public interface IPantryStore
{
    Task<IReadOnlyList<PantryItem>> ListAsync(string owner, PantryQuery query, CancellationToken token);
    Task<PantryItem> AddAsync(PantryItem item, CancellationToken token);
    Task<bool> UpdateAsync(string owner, Guid id, PantryRequest request, byte[] version, CancellationToken token);
    Task<bool> ChangeAsync(string owner, Guid id, byte[]? version, bool consume, CancellationToken token);
    Task<bool> ExistsAsync(string owner, Guid id, CancellationToken token);
    Task<bool> DeleteAsync(string owner, Guid id, CancellationToken token);
}

public sealed class PantryStore(PantryRepository repository) : IPantryStore
{
    public async Task<IReadOnlyList<PantryItem>> ListAsync(string owner, PantryQuery query, CancellationToken token) =>
        await repository.ListFilteredAsync(owner, query.Search, query.Type, query.Availability,
            query.ExpiresBefore, query.Page, query.PageSize, token);
    public Task<PantryItem> AddAsync(PantryItem item, CancellationToken token) => repository.AddAsync(item, token);
    public Task<bool> UpdateAsync(string owner, Guid id, PantryRequest request, byte[] version, CancellationToken token) =>
        repository.UpdateAsync(owner, id, request.Name, request.Type, request.CustomCategory,
            request.Notes, request.Quantity, request.Unit, request.ExpiresAt, version, token);
    public Task<bool> ChangeAsync(string owner, Guid id, byte[]? version, bool consume, CancellationToken token) =>
        consume ? repository.ConsumeAsync(owner, id, version, token) : repository.MarkUnavailableAsync(owner, id, version, token);
    public Task<bool> ExistsAsync(string owner, Guid id, CancellationToken token) => repository.ExistsAsync(owner, id, token);
    public Task<bool> DeleteAsync(string owner, Guid id, CancellationToken token) => repository.DeleteAsync(owner, id, token);
}
