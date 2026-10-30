namespace MealPlanner.Data.Events;

public sealed class OutboxMessage
{
    private static readonly System.Text.Json.JsonSerializerOptions PayloadJsonOptions =
        new(System.Text.Json.JsonSerializerDefaults.Web);

    private OutboxMessage()
    {
    }

    public Guid Id { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public int SchemaVersion { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public string SourceService { get; private set; } = string.Empty;

    public string CorrelationId { get; private set; } = string.Empty;

    public string Payload { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset NextAttemptAt { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public DateTimeOffset? DeadLetteredAt { get; private set; }

    public int AttemptCount { get; private set; }

    public string? LastError { get; private set; }

    public static OutboxMessage Create(MealPlanEvent mealPlanEvent)
    {
        return new OutboxMessage
        {
            Id = mealPlanEvent.EventId,
            EventType = mealPlanEvent.EventType,
            SchemaVersion = mealPlanEvent.SchemaVersion,
            OccurredAtUtc = mealPlanEvent.OccurredAtUtc,
            SourceService = mealPlanEvent.SourceService,
            CorrelationId = mealPlanEvent.CorrelationId,
            Payload = System.Text.Json.JsonSerializer.Serialize(mealPlanEvent.Payload, PayloadJsonOptions),
            CreatedAt = mealPlanEvent.OccurredAtUtc,
            NextAttemptAt = mealPlanEvent.OccurredAtUtc,
        };
    }

    public void MarkPublished(DateTimeOffset now)
    {
        PublishedAt = now;
        LastError = null;
    }

    public void RecordFailure(string errorType, DateTimeOffset now)
    {
        AttemptCount++;
        LastError = errorType.Length <= 64 ? errorType : errorType[..64];
        if (AttemptCount >= 3)
        {
            DeadLetteredAt = now;
            return;
        }

        NextAttemptAt = now.AddSeconds(Math.Pow(2, AttemptCount));
    }
}
