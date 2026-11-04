using System.Text.Json;
using ChopChop.Shared;
using InteractiveReview.Api.Contracts;
using InteractiveReview.Data;
using InteractiveReview.Data.Repositories;
using InteractiveReview.Domain;

namespace InteractiveReview.Api.Services;

public sealed class ReviewApplicationService(
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions EventJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ReviewResponse> CreateAsync(
        Guid ownerId,
        CreateReviewRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var queries = unitOfWork.CustomRepository<IReviewQueries>();
        if (await queries.GetByOwnerAndRecipeAsync(ownerId, request.RecipeId, cancellationToken) is not null)
            throw new DuplicateReviewException();

        var review = Review.Create(
            Guid.NewGuid(),
            request.RecipeId,
            ownerId,
            request.Rating,
            request.Body,
            request.Suggestion,
            timeProvider.GetUtcNow());

        await unitOfWork.Repository<Review>().AddAsync(review, cancellationToken);
        await WriteReviewEventAsync(review, "submitted", "interactive_review.review.submitted", correlationId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(review);
    }

    public async Task<ReviewResponse?> GetOwnedAsync(
        Guid reviewId,
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        var review = await unitOfWork.CustomRepository<IReviewQueries>()
            .GetOwnedAsync(reviewId, ownerId, cancellationToken);
        return review is null ? null : ToResponse(review);
    }

    public async Task<IReadOnlyList<ReviewResponse>> GetPublishedForRecipeAsync(
        Guid recipeId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (page is < 1 or > 10_000 || pageSize is < 1 or > 50)
            throw new ArgumentOutOfRangeException(nameof(page), "Page must be 1..10000 and page size 1..50.");

        var reviews = await unitOfWork.CustomRepository<IReviewQueries>()
            .GetPublishedForRecipeAsync(recipeId, page, pageSize, cancellationToken);
        return reviews.Select(ToResponse).ToArray();
    }

    public async Task<IReadOnlyList<ReviewResponse>> GetPendingForModerationAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        if (limit is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 100.");

        var reviews = await unitOfWork.CustomRepository<IReviewQueries>()
            .GetPendingAsync(limit, cancellationToken);
        return reviews.Select(ToResponse).ToArray();
    }

    public async Task<bool> UpdateAsync(
        Guid reviewId,
        Guid ownerId,
        UpdateReviewRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var review = await unitOfWork.CustomRepository<IReviewQueries>()
            .GetOwnedAsync(reviewId, ownerId, cancellationToken);
        if (review is null)
            return false;

        review.Update(ownerId, request.Rating, request.Body, request.Suggestion, timeProvider.GetUtcNow());
        unitOfWork.Repository<Review>().Update(review);
        await WriteReviewEventAsync(review, "updated", "interactive_review.review.updated", correlationId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(
        Guid reviewId,
        Guid ownerId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var review = await unitOfWork.CustomRepository<IReviewQueries>()
            .GetOwnedAsync(reviewId, ownerId, cancellationToken);
        if (review is null)
            return false;

        await WriteReviewEventAsync(
            review,
            "deleted",
            "interactive_review.review.deleted",
            correlationId,
            cancellationToken,
            includeStatus: false);
        unitOfWork.Repository<Review>().Remove(review);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ModerateAsync(
        Guid reviewId,
        Guid moderatorId,
        ModerateReviewRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var review = await unitOfWork.CustomRepository<IReviewQueries>().GetByIdAsync(reviewId, cancellationToken);
        if (review is null)
            return false;

        review.Moderate(moderatorId, request.Status, request.Reason, timeProvider.GetUtcNow());
        var eventAction = request.Status.ToString().ToLowerInvariant();
        await WriteReviewEventAsync(
            review,
            eventAction,
            $"interactive_review.review.{eventAction}",
            correlationId,
            cancellationToken);
        unitOfWork.Repository<Review>().Update(review);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static ReviewResponse ToResponse(Review review) =>
        new(
            review.Id,
            review.RecipeId,
            review.OwnerId,
            review.Rating,
            review.Body,
            review.Suggestion,
            review.Status,
            review.CreatedAtUtc,
            review.UpdatedAtUtc);

    private async Task WriteReviewEventAsync(
        Review review,
        string action,
        string eventName,
        string correlationId,
        CancellationToken cancellationToken,
        bool includeStatus = true)
    {
        var envelope = new EventEnvelope<ReviewChanged>(
            Guid.NewGuid(),
            eventName,
            1,
            timeProvider.GetUtcNow(),
            "interactive-review",
            correlationId,
            new ReviewChanged(
                review.Id,
                review.RecipeId,
                review.OwnerId,
                review.Rating,
                action,
                includeStatus ? review.Status : null));
        var message = new OutboxMessage(
            envelope.EventId,
            envelope.EventType,
            eventName,
            JsonSerializer.Serialize(envelope, EventJsonOptions),
            correlationId,
            envelope.OccurredAtUtc);
        await unitOfWork.CustomRepository<IOutboxWriter>().AddAsync(message, cancellationToken);
    }

    public sealed record ReviewChanged(
        Guid ReviewId,
        Guid RecipeId,
        Guid OwnerId,
        int Rating,
        string Action,
        ReviewStatus? Status);
}

public sealed class DuplicateReviewException : Exception
{
}
