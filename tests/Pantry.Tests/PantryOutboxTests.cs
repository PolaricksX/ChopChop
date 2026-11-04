using Microsoft.EntityFrameworkCore;
using Pantry.Data.Context;
using Pantry.Data.Persistence;
using Pantry.Data.Repositories;

namespace Pantry.Tests;

public sealed class PantryOutboxTests
{
    [Fact]
    public async Task Unpublished_messages_are_ordered_and_marking_is_idempotently_addressable()
    {
        await using var db = CreateContext();
        var repository = new OutboxRepository(db);
        var first = new OutboxMessage(Guid.NewGuid(), "PantryItemAdded", "{}", DateTimeOffset.UtcNow.AddMinutes(-2), "corr-1");
        var second = new OutboxMessage(Guid.NewGuid(), "PantryItemConsumed", "{}", DateTimeOffset.UtcNow.AddMinutes(-1), "corr-1");
        await repository.AddAsync(second, CancellationToken.None);
        await repository.AddAsync(first, CancellationToken.None);

        var pending = await repository.GetUnpublishedAsync(1, CancellationToken.None);

        Assert.Equal(first.Id, Assert.Single(pending).Id);
        Assert.True(await repository.MarkPublishedAsync(first.Id, DateTimeOffset.UtcNow, CancellationToken.None));
        Assert.False(await repository.MarkPublishedAsync(Guid.NewGuid(), DateTimeOffset.UtcNow, CancellationToken.None));
        Assert.Equal(second.Id, Assert.Single(await repository.GetUnpublishedAsync(10, CancellationToken.None)).Id);
    }

    [Fact]
    public async Task Pantry_mutations_write_transactional_outbox_events()
    {
        await using var db = CreateContext();
        var repository = new PantryRepository(db);

        await repository.AddAsync(new Pantry.Domain.Entities.PantryItem(
            Guid.NewGuid(), "owner", "Rice", Pantry.Domain.Enums.PantryType.Grains, 1,
            Pantry.Domain.Enums.Unit.Kilogram), CancellationToken.None);

        var eventMessage = Assert.Single(await db.OutboxMessages
            .Where(message => message.Type == "PantryItemAdded")
            .ToListAsync());
        Assert.Contains("\"Name\":\"Rice\"", eventMessage.Payload);
        Assert.Equal("system", eventMessage.CorrelationId);
    }

    [Fact]
    public void Outbox_rejects_missing_required_fields()
    {
        Assert.Throws<ArgumentException>(() => new OutboxMessage(Guid.NewGuid(), " ", "{}", DateTimeOffset.UtcNow, "c"));
        Assert.Throws<ArgumentException>(() => new OutboxMessage(Guid.NewGuid(), "Event", "{}", DateTimeOffset.UtcNow, " "));
        Assert.Throws<ArgumentNullException>(() => new OutboxMessage(Guid.NewGuid(), "Event", null!, DateTimeOffset.UtcNow, "c"));
    }

    private static PantryDbContext CreateContext() => new(new DbContextOptionsBuilder<PantryDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
