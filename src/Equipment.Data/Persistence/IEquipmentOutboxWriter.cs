using ChopChop.Shared;

namespace Equipment.Data.Persistence;

public interface IEquipmentOutboxWriter
{
    Task AddAsync<T>(
        string routingKey,
        EventEnvelope<T> envelope,
        CancellationToken cancellationToken);
}
