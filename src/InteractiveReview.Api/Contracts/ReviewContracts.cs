using InteractiveReview.Domain;

namespace InteractiveReview.Api.Contracts;

public sealed record CreateReviewRequest(Guid RecipeId, int Rating, string Body, string? Suggestion);

public sealed record UpdateReviewRequest(int Rating, string Body, string? Suggestion);

public sealed record ModerateReviewRequest(ReviewStatus Status, string Reason);

public sealed record ReviewResponse(
    Guid Id,
    Guid RecipeId,
    Guid OwnerId,
    int Rating,
    string Body,
    string? Suggestion,
    ReviewStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
