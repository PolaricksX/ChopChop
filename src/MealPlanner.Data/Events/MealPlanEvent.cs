namespace MealPlanner.Data.Events;

public sealed record MealPlanEvent(
    Guid EventId,
    string EventType,
    int SchemaVersion,
    DateTimeOffset OccurredAtUtc,
    string SourceService,
    string CorrelationId,
    object Payload);
