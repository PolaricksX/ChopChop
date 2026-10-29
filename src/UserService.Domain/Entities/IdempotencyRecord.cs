namespace UserService.Domain.Entities;

/// <summary>Stores the outcome of a create command so a repeated <c>Idempotency-Key</c> returns the same result.</summary>
public sealed class IdempotencyRecord
{
    private IdempotencyRecord()
    {
    }

    public Guid Id { get; private set; }

    public string Subject { get; private set; } = string.Empty;

    public string Key { get; private set; } = string.Empty;

    public string RequestHash { get; private set; } = string.Empty;

    public Guid UserProfileId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static IdempotencyRecord Create(string subject, string key, string requestHash, Guid userProfileId, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            Subject = subject,
            Key = key,
            RequestHash = requestHash,
            UserProfileId = userProfileId,
            CreatedAt = now,
        };
}
