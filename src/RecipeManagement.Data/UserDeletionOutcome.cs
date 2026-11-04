namespace RecipeManagement.Data;

public sealed class UserDeletionOutcome
{
    private UserDeletionOutcome() { }
    public UserDeletionOutcome(Guid eventId, Guid userId, int draftsDeleted, int recipesAnonymized, DateTimeOffset processedAtUtc)
    { EventId = eventId; UserId = userId; DraftsDeleted = draftsDeleted; RecipesAnonymized = recipesAnonymized; ProcessedAtUtc = processedAtUtc.ToUniversalTime(); }
    public Guid EventId { get; private set; }
    public Guid UserId { get; private set; }
    public int DraftsDeleted { get; private set; }
    public int RecipesAnonymized { get; private set; }
    public DateTimeOffset ProcessedAtUtc { get; private set; }
}
