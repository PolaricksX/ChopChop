namespace Equipment.Data.Persistence;

public sealed class EquipmentIdempotencyRecord
{
    private EquipmentIdempotencyRecord()
    {
    }

    public EquipmentIdempotencyRecord(
        string ownerId,
        string key,
        string requestHash,
        Guid resourceId,
        DateTimeOffset createdAtUtc)
    {
        OwnerId = string.IsNullOrWhiteSpace(ownerId)
            ? throw new ArgumentException("An owner id is required.", nameof(ownerId))
            : ownerId;
        Key = string.IsNullOrWhiteSpace(key) || key.Length > 128
            ? throw new ArgumentException("A valid idempotency key is required.", nameof(key))
            : key;
        RequestHash = string.IsNullOrWhiteSpace(requestHash) || requestHash.Length != 64
            ? throw new ArgumentException("A SHA-256 request hash is required.", nameof(requestHash))
            : requestHash;
        if (resourceId == Guid.Empty)
        {
            throw new ArgumentException("A resource id is required.", nameof(resourceId));
        }

        Id = Guid.NewGuid();
        ResourceId = resourceId;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
    }

    public Guid Id { get; private set; }

    public string OwnerId { get; private set; } = string.Empty;

    public string Key { get; private set; } = string.Empty;

    public string RequestHash { get; private set; } = string.Empty;

    public Guid ResourceId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
}
