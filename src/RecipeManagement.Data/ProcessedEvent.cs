namespace RecipeManagement.Data;

public sealed class ProcessedEvent
{
    private ProcessedEvent() { }
    public ProcessedEvent(Guid eventId, DateTimeOffset processedAtUtc) { EventId = eventId; ProcessedAtUtc = processedAtUtc.ToUniversalTime(); }
    public Guid EventId { get; private set; }
    public DateTimeOffset ProcessedAtUtc { get; private set; }
}
