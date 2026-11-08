using Equipment.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Equipment.Data.Persistence;

internal sealed class EquipmentOutboxQueries(EquipmentDbContext context) : IEquipmentOutboxQueries
{
    public async Task<IReadOnlyList<EquipmentOutboxMessage>> GetPendingAsync(
        int limit,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken) =>
        await context.OutboxMessages
            .Where(message =>
                message.PublishedAtUtc == null &&
                message.DeadLetteredAtUtc == null &&
                (message.NextAttemptAtUtc == null || message.NextAttemptAtUtc <= nowUtc))
            .OrderBy(message => message.OccurredAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
}
