using UserPantry.Application.Models;
using UserPantry.Domain;

namespace UserPantry.Application.Abstractions;

public interface IExpiringPantryItemsQuery
{
    Task<PagedResult<PantryItem>> ExecuteAsync(
        Guid ownerId,
        DateTimeOffset startsAtInclusive,
        DateTimeOffset endsAtExclusive,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
