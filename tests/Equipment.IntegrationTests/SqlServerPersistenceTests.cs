using Equipment.Data.Persistence;
using Equipment.Data.Repositories;
using Equipment.Domain.Entities;
using Equipment.Domain.Enums;
using Equipment.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Equipment.IntegrationTests;

public sealed class SqlServerPersistenceTests
{
    [SqlServerFact]
    public async Task Migration_preserves_owner_isolation_rowversion_and_outbox_atomicity()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        var equipmentId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using (var context = database.CreateContext())
        {
            context.EquipmentItems.Add(new EquipmentItem(
                equipmentId,
                "Owner-A",
                "Cast iron pan",
                EquipmentType.Cookware,
                1,
                now));
            context.EquipmentItems.Add(new EquipmentItem(
                Guid.NewGuid(),
                "owner-a",
                "Cast iron pan",
                EquipmentType.Cookware,
                1,
                now));
            await context.SaveChangesAsync();
            var inserted = context.EquipmentItems.Local.Single(item => item.Id == equipmentId);
            Assert.Equal(8, context.Entry(inserted)
                .Property(item => item.RowVersion).CurrentValue!.Length);

            var page = await new EquipmentRepository(context).ListOwnedAsync(
                "Owner-A",
                new EquipmentQuery(1, 20, null, null, null, null),
                CancellationToken.None);
            Assert.Equal(1, page.TotalCount);
            Assert.Equal("Owner-A", Assert.Single(page.Items).OwnerId);
        }

        await using (var failedUnit = database.CreateContext())
        {
            failedUnit.EquipmentItems.Add(new EquipmentItem(
                Guid.NewGuid(),
                "Owner-A",
                "CAST IRON PAN",
                EquipmentType.Cookware,
                1,
                now));
            failedUnit.OutboxMessages.Add(EquipmentOutboxMessage.Create(
                "equipment.added",
                new ChopChop.Shared.EventEnvelope<TestEvent>(
                    Guid.NewGuid(),
                    "EquipmentAdded",
                    1,
                    now,
                    "equipment",
                    "sql-test-correlation",
                    new TestEvent(Guid.NewGuid()))));

            await Assert.ThrowsAsync<DbUpdateException>(() => failedUnit.SaveChangesAsync());
        }

        await using (var verify = database.CreateContext())
        {
            Assert.Equal(0, await verify.OutboxMessages.CountAsync());
        }

        await using var left = database.CreateContext();
        await using var right = database.CreateContext();
        var firstCopy = await left.EquipmentItems.SingleAsync(item => item.Id == equipmentId);
        var staleCopy = await right.EquipmentItems.SingleAsync(item => item.Id == equipmentId);
        firstCopy.SetQuantity(2, now.AddMinutes(1));
        await left.SaveChangesAsync();
        staleCopy.SetQuantity(3, now.AddMinutes(2));

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => right.SaveChangesAsync());
    }

    private sealed record TestEvent(Guid EquipmentId);
}
