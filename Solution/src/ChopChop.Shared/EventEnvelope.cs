namespace ChopChop.Shared;

public sealed record EventEnvelope<T>(
    Guid EventId,
    string EventType,
    int SchemaVersion,
    DateTimeOffset OccurredAtUtc,
    string SourceService,
    string CorrelationId,
    T Payload);

public interface IEventBus
{
    Task PublishAsync<T>(
        string routingKey,
        EventEnvelope<T> message,
        CancellationToken cancellationToken);

    Task PublishSerializedAsync(
        string routingKey,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken);
}
