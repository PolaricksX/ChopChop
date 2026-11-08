using System.Text.Json;
using ChopChop.Shared;

namespace Equipment.Data.Persistence;

public sealed class EquipmentOutboxMessage
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private EquipmentOutboxMessage()
    {
    }

    public Guid Id { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public string RoutingKey { get; private set; } = string.Empty;

    public string Payload { get; private set; } = string.Empty;

    public string CorrelationId { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public DateTimeOffset? PublishedAtUtc { get; private set; }

    public DateTimeOffset? NextAttemptAtUtc { get; private set; }

    public DateTimeOffset? DeadLetteredAtUtc { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    public static EquipmentOutboxMessage Create<T>(string routingKey, EventEnvelope<T> envelope)
    {
        if (string.IsNullOrWhiteSpace(routingKey))
        {
            throw new ArgumentException("A routing key is required.", nameof(routingKey));
        }

        return new EquipmentOutboxMessage
        {
            Id = envelope.EventId,
            EventType = envelope.EventType,
            RoutingKey = routingKey.Trim(),
            Payload = JsonSerializer.Serialize(envelope, JsonOptions),
            CorrelationId = envelope.CorrelationId,
            OccurredAtUtc = envelope.OccurredAtUtc.ToUniversalTime(),
            NextAttemptAtUtc = envelope.OccurredAtUtc.ToUniversalTime(),
        };
    }

    public void MarkPublished(DateTimeOffset publishedAtUtc)
    {
        PublishedAtUtc = publishedAtUtc.ToUniversalTime();
        LastError = null;
    }

    public void MarkFailed(string errorType, DateTimeOffset failedAtUtc)
    {
        Attempts++;
        LastError = errorType.Length <= 64 ? errorType : errorType[..64];
        var failedAt = failedAtUtc.ToUniversalTime();
        if (Attempts >= 3)
        {
            DeadLetteredAtUtc = failedAt;
            return;
        }

        NextAttemptAtUtc = failedAt.AddSeconds(Math.Pow(2, Attempts));
    }
}
