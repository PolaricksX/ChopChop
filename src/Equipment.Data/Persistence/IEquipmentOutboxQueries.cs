namespace Equipment.Data.Persistence;

public interface IEquipmentOutboxQueries
{
    Task<IReadOnlyList<EquipmentOutboxMessage>> GetPendingAsync(
        int limit,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);
}
