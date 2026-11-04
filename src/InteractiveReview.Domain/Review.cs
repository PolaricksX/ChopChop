namespace InteractiveReview.Domain;

public sealed class Review
{
    private Review()
    {
        Body = string.Empty;
    }

    private Review(
        Guid id,
        Guid recipeId,
        Guid ownerId,
        int rating,
        string body,
        string? suggestion,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        RecipeId = recipeId;
        OwnerId = ownerId;
        Rating = rating;
        Body = body;
        Suggestion = suggestion;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
        Status = ReviewStatus.Pending;
        Version = Guid.NewGuid();
    }

    public Guid Id { get; private set; }
    public Guid RecipeId { get; private set; }
    public Guid OwnerId { get; private set; }
    public int Rating { get; private set; }
    public string Body { get; private set; }
    public string? Suggestion { get; private set; }
    public ReviewStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? ModeratedAtUtc { get; private set; }
    public Guid? ModeratedBy { get; private set; }
    public string? ModerationReason { get; private set; }
    public Guid Version { get; private set; }

    public static Review Create(
        Guid id,
        Guid recipeId,
        Guid ownerId,
        int rating,
        string body,
        string? suggestion,
        DateTimeOffset nowUtc)
    {
        Validate(id, recipeId, ownerId, rating, body, suggestion);
        return new Review(id, recipeId, ownerId, rating, body.Trim(), Normalize(suggestion), nowUtc.ToUniversalTime());
    }

    public void Update(
        Guid ownerId,
        int rating,
        string body,
        string? suggestion,
        DateTimeOffset nowUtc)
    {
        if (ownerId != OwnerId)
            throw new InvalidOperationException("Only the review owner can update it.");

        Validate(Id, RecipeId, OwnerId, rating, body, suggestion);

        Rating = rating;
        Body = body.Trim();
        Suggestion = Normalize(suggestion);
        Status = ReviewStatus.Pending;
        ModeratedAtUtc = null;
        ModeratedBy = null;
        ModerationReason = null;
        UpdatedAtUtc = nowUtc.ToUniversalTime();
        Version = Guid.NewGuid();
    }

    public void Moderate(Guid moderatorId, ReviewStatus status, string reason, DateTimeOffset nowUtc)
    {
        if (moderatorId == Guid.Empty)
            throw new ArgumentException("Moderator identifier is required.", nameof(moderatorId));

        var transitionAllowed =
            (Status == ReviewStatus.Pending && status is ReviewStatus.Published or ReviewStatus.Rejected) ||
            (Status == ReviewStatus.Published && status == ReviewStatus.Hidden);
        if (!transitionAllowed)
            throw new InvalidOperationException("The requested moderation transition is not permitted.");

        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
            throw new ArgumentException("A moderation reason of at most 500 characters is required.", nameof(reason));

        Status = status;
        ModeratedBy = moderatorId;
        ModeratedAtUtc = nowUtc.ToUniversalTime();
        ModerationReason = reason.Trim();
        UpdatedAtUtc = nowUtc.ToUniversalTime();
        Version = Guid.NewGuid();
    }

    private static void Validate(Guid id, Guid recipeId, Guid ownerId, int rating, string body, string? suggestion)
    {
        if (id == Guid.Empty || recipeId == Guid.Empty || ownerId == Guid.Empty)
            throw new ArgumentException("Review, recipe, and owner identifiers are required.");

        if (rating is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 1 and 5.");

        if (string.IsNullOrWhiteSpace(body) || body.Length > 2_000)
            throw new ArgumentException("Review text must contain 1 to 2000 characters.", nameof(body));

        if (suggestion is { Length: > 1_000 })
            throw new ArgumentException("Suggestions must not exceed 1000 characters.", nameof(suggestion));
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
