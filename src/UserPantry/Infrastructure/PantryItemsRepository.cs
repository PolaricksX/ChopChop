using Microsoft.EntityFrameworkCore;
using UserPantry.Application.Abstractions;
using UserPantry.Application.Models;
using UserPantry.Domain;

namespace UserPantry.Infrastructure;

public sealed class PantryItemsRepository(PantryDbContext dbContext) : IExpiringPantryItemsQuery
{
    public async Task<PagedResult<PantryItem>> ExecuteAsync(
        Guid ownerId,
        DateTimeOffset startsAtInclusive,
        DateTimeOffset endsAtExclusive,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var matchingItems = dbContext.PantryItems
            .AsNoTracking()
            .Where(item =>
                item.OwnerId == ownerId
                && item.Expiration != null
                && item.Expiration >= startsAtInclusive
                && item.Expiration < endsAtExclusive);

        var totalCount = await matchingItems.CountAsync(cancellationToken);
        var skip = checked((page - 1) * pageSize);
        var items = await matchingItems
            .OrderBy(item => item.Expiration)
            .ThenBy(item => item.Name)
            .ThenBy(item => item.Id)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<PantryItem>(items, page, pageSize, totalCount);
    }
}
