namespace MealPlanner.Domain.Entities;

public sealed class MealPlanIdempotencyRecord
{
    private MealPlanIdempotencyRecord()
    {
    }

    public Guid Id { get; private set; }

    public string OwnerId { get; private set; } = string.Empty;

    public string Key { get; private set; } = string.Empty;

    public string RequestHash { get; private set; } = string.Empty;

    public string ResourceType { get; private set; } = string.Empty;

    public Guid ResourceId { get; private set; }

    public Guid? ParentId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static MealPlanIdempotencyRecord Create(
        string ownerId,
        string key,
        string requestHash,
        string resourceType,
        Guid resourceId,
        Guid? parentId,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Key = key,
            RequestHash = requestHash,
            ResourceType = resourceType,
            ResourceId = resourceId,
            ParentId = parentId,
            CreatedAt = now,
        };
}
