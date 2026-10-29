using InteractiveReview.Domain;

namespace InteractiveReview.Data.Repositories;

public interface IReviewQueries
{
    Task<Review?> GetOwnedAsync(Guid reviewId, Guid ownerId, CancellationToken cancellationToken);
    Task<Review?> GetByIdAsync(Guid reviewId, CancellationToken cancellationToken);
    Task<Review?> GetByOwnerAndRecipeAsync(Guid ownerId, Guid recipeId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Review>> GetPublishedForRecipeAsync(
        Guid recipeId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<Review>> GetPendingAsync(int limit, CancellationToken cancellationToken);
}
