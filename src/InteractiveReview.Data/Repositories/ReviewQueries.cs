using InteractiveReview.Data.Context;
using InteractiveReview.Domain;
using Microsoft.EntityFrameworkCore;

namespace InteractiveReview.Data.Repositories;

internal sealed class ReviewQueries(InteractiveReviewDbContext context) : IReviewQueries
{
    public Task<Review?> GetOwnedAsync(
        Guid reviewId,
        Guid ownerId,
        CancellationToken cancellationToken) =>
        context.Reviews.SingleOrDefaultAsync(
            review => review.Id == reviewId && review.OwnerId == ownerId,
            cancellationToken);

    public Task<Review?> GetByIdAsync(Guid reviewId, CancellationToken cancellationToken) =>
        context.Reviews.SingleOrDefaultAsync(review => review.Id == reviewId, cancellationToken);

    public Task<Review?> GetByOwnerAndRecipeAsync(
        Guid ownerId,
        Guid recipeId,
        CancellationToken cancellationToken) =>
        context.Reviews.SingleOrDefaultAsync(
            review => review.OwnerId == ownerId && review.RecipeId == recipeId,
            cancellationToken);

    public async Task<IReadOnlyList<Review>> GetPublishedForRecipeAsync(
        Guid recipeId,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        await context.Reviews
            .AsNoTracking()
            .Where(review => review.RecipeId == recipeId && review.Status == ReviewStatus.Published)
            .OrderByDescending(review => review.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Review>> GetPendingAsync(
        int limit,
        CancellationToken cancellationToken) =>
        await context.Reviews
            .AsNoTracking()
            .Where(review => review.Status == ReviewStatus.Pending)
            .OrderBy(review => review.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
}
