using Microsoft.EntityFrameworkCore;
using Pantry.Data.Context;
using Pantry.Data.Persistence;

namespace Pantry.Data.Repositories;

public interface IOutboxStore
{
    Task AddAsync(OutboxMessage message, CancellationToken token);
    Task<IReadOnlyList<OutboxMessage>> GetUnpublishedAsync(int take, CancellationToken token);
    Task<bool> MarkPublishedAsync(Guid id, DateTimeOffset publishedAt, CancellationToken token);
}

public sealed class OutboxRepository(PantryDbContext db) : IOutboxStore
{
    public async Task AddAsync(OutboxMessage message, CancellationToken token)
    {
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync(token);
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetUnpublishedAsync(int take, CancellationToken token) =>
        await db.OutboxMessages
            .Where(message => message.PublishedAt == null)
            .OrderBy(message => message.OccurredAt)
            .ThenBy(message => message.Id)
            .Take(Math.Max(take, 1))
            .ToListAsync(token);

    public async Task<bool> MarkPublishedAsync(Guid id, DateTimeOffset publishedAt, CancellationToken token)
    {
        var message = await db.OutboxMessages.SingleOrDefaultAsync(item => item.Id == id, token);
        if (message is null)
            return false;
        message.MarkPublished(publishedAt);
        await db.SaveChangesAsync(token);
        return true;
    }
}
