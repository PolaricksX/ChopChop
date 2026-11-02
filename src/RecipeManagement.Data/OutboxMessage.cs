namespace RecipeManagement.Data;

public sealed class OutboxMessage
{
    private OutboxMessage() { EventType = RoutingKey = Payload = CorrelationId = string.Empty; }
    public OutboxMessage(Guid id, string eventType, string routingKey, string payload, string correlationId, DateTimeOffset occurredAtUtc)
    { Id = id; EventType = eventType; RoutingKey = routingKey; Payload = payload; CorrelationId = correlationId; OccurredAtUtc = occurredAtUtc.ToUniversalTime(); }
    public Guid Id { get; private set; }
    public string EventType { get; private set; }
    public string RoutingKey { get; private set; }
    public string Payload { get; private set; }
    public string CorrelationId { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public DateTimeOffset? PublishedAtUtc { get; private set; }
    public DateTimeOffset? NextAttemptAtUtc { get; private set; }
    public DateTimeOffset? DeadLetteredAtUtc { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }
    public void MarkPublished(DateTimeOffset timestamp) { PublishedAtUtc = timestamp.ToUniversalTime(); LastError = null; }
    public void MarkFailed(string error, DateTimeOffset timestamp)
    { Attempts++; LastError = error[..Math.Min(error.Length, 500)]; var time = timestamp.ToUniversalTime(); if (Attempts >= 8) DeadLetteredAtUtc = time; else NextAttemptAtUtc = time.AddMinutes(Math.Pow(2, Attempts - 1)); }
}
