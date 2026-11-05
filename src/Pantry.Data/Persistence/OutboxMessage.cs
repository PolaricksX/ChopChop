namespace Pantry.Data.Persistence;

public sealed class OutboxMessage
{
    private OutboxMessage() { }

    public OutboxMessage(
        Guid id,
        string type,
        string payload,
        DateTimeOffset occurredAt,
        string correlationId)
    {
        Id = id;
        Type = string.IsNullOrWhiteSpace(type) ? throw new ArgumentException("A type is required.", nameof(type)) : type.Trim();
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        OccurredAt = occurredAt.ToUniversalTime();
        CorrelationId = string.IsNullOrWhiteSpace(correlationId)
            ? throw new ArgumentException("A correlation id is required.", nameof(correlationId))
            : correlationId.Trim();
    }

    public Guid Id { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public DateTimeOffset? PublishedAt { get; private set; }

    public void MarkPublished(DateTimeOffset publishedAt) => PublishedAt = publishedAt.ToUniversalTime();
}
