namespace RecipeManagement.Data;

public sealed class IdempotencyRecord
{
    private IdempotencyRecord() { Key = RequestFingerprint = ResponsePayload = string.Empty; }
    public IdempotencyRecord(Guid ownerId, string key, string requestFingerprint, Guid resourceId, string responsePayload, int statusCode)
    { Id = Guid.NewGuid(); OwnerId = ownerId; Key = key; RequestFingerprint = requestFingerprint; ResourceId = resourceId; ResponsePayload = responsePayload; StatusCode = statusCode; CreatedAtUtc = DateTimeOffset.UtcNow; }
    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public string Key { get; private set; }
    public string RequestFingerprint { get; private set; }
    public Guid ResourceId { get; private set; }
    public string ResponsePayload { get; private set; }
    public int StatusCode { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
