using Equipment.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Equipment.Data.Persistence;

internal sealed class EquipmentOutboxWriter(EquipmentDbContext context) : IEquipmentOutboxWriter
{
    public async Task AddAsync<T>(
        string routingKey,
        ChopChop.Shared.EventEnvelope<T> envelope,
        CancellationToken cancellationToken) =>
        await context.OutboxMessages.AddAsync(
            EquipmentOutboxMessage.Create(routingKey, envelope),
            cancellationToken);
}
