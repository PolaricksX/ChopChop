using AiRecipeGeneration.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace AiRecipeGeneration.Data.Repositories;

internal sealed class OutboxQueries(AiRecipeDbContext context) : IOutboxQueries
{
    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(
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
