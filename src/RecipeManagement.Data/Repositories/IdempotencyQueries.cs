using Microsoft.EntityFrameworkCore;
using RecipeManagement.Data.Context;

namespace RecipeManagement.Data.Repositories;

internal sealed class IdempotencyQueries(RecipeManagementDbContext context) : IIdempotencyQueries
{
    public Task<IdempotencyRecord?> FindAsync(Guid ownerId, string key, CancellationToken cancellationToken) =>
        context.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == ownerId && x.Key == key, cancellationToken);
    public Task AddAsync(IdempotencyRecord record, CancellationToken cancellationToken) => context.IdempotencyRecords.AddAsync(record, cancellationToken).AsTask();
}
