using InteractiveReview.Api.Contracts;
using InteractiveReview.Api.Services;
using InteractiveReview.Data;
using InteractiveReview.Data.Repositories;
using InteractiveReview.Domain;

namespace InteractiveReview.UnitTests;

public sealed class ReviewApplicationServiceTests
{
    private readonly Guid owner = Guid.NewGuid();
    private readonly Guid stranger = Guid.NewGuid();
    private readonly Review review;
    private readonly FakeUnitOfWork unitOfWork;
    private readonly ReviewApplicationService service;

    public ReviewApplicationServiceTests()
    {
        review = Review.Create(Guid.NewGuid(), Guid.NewGuid(), owner, 4, "Nice recipe", null, DateTimeOffset.UtcNow);
        unitOfWork = new FakeUnitOfWork(review);
        service = new ReviewApplicationService(unitOfWork, TimeProvider.System);
    }

    [Fact]
    public async Task Another_user_cannot_read_update_or_delete_a_review()
    {
        Assert.Null(await service.GetOwnedAsync(review.Id, stranger, CancellationToken.None));
        Assert.False(await service.UpdateAsync(review.Id, stranger, new UpdateReviewRequest(1, "Hijacked", null), "c", CancellationToken.None));
        Assert.False(await service.DeleteAsync(review.Id, stranger, "c", CancellationToken.None));

        Assert.Equal(0, unitOfWork.Saves);
        Assert.Equal(4, review.Rating);
        Assert.NotNull(await service.GetOwnedAsync(review.Id, owner, CancellationToken.None));
    }

    [Fact]
    public async Task Moderator_cannot_moderate_their_own_review()
    {
        await Assert.ThrowsAsync<SelfModerationException>(() => service.ModerateAsync(
            review.Id, owner, new ModerateReviewRequest(ReviewStatus.Published, "Self-approve"), "c", CancellationToken.None));

        Assert.Equal(ReviewStatus.Pending, review.Status);
        Assert.Equal(0, unitOfWork.Saves);
    }

    [Fact]
    public async Task Moderator_can_moderate_another_users_review()
    {
        Assert.True(await service.ModerateAsync(
            review.Id, stranger, new ModerateReviewRequest(ReviewStatus.Published, "Approved"), "c", CancellationToken.None));

        Assert.Equal(ReviewStatus.Published, review.Status);
        Assert.Equal(1, unitOfWork.Saves);
    }

    private sealed class FakeUnitOfWork(Review review) : IUnitOfWork
    {
        public int Saves { get; private set; }

        public IRepository<T> Repository<T>() where T : class => new NoOpRepository<T>();

        public T CustomRepository<T>() where T : class => typeof(T) switch
        {
            var type when type == typeof(IReviewQueries) => (T)(object)new FakeQueries(review),
            var type when type == typeof(IOutboxWriter) => (T)(object)new NoOpOutbox(),
            _ => throw new NotSupportedException(typeof(T).Name),
        };

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(++Saves);
    }

    // Mirrors the real owner-filtered queries so the test proves the service passes the caller's id.
    private sealed class FakeQueries(Review review) : IReviewQueries
    {
        public Task<Review?> GetOwnedAsync(Guid reviewId, Guid ownerId, CancellationToken cancellationToken) =>
            Task.FromResult<Review?>(review.Id == reviewId && review.OwnerId == ownerId ? review : null);

        public Task<Review?> GetByIdAsync(Guid reviewId, CancellationToken cancellationToken) =>
            Task.FromResult<Review?>(review.Id == reviewId ? review : null);

        public Task<Review?> GetByOwnerAndRecipeAsync(Guid ownerId, Guid recipeId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Review>> GetPublishedForRecipeAsync(Guid recipeId, int page, int pageSize, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Review>> GetPendingAsync(int limit, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class NoOpRepository<T> : IRepository<T> where T : class
    {
        public Task AddAsync(T entity, CancellationToken cancellationToken) => Task.CompletedTask;

        public void Update(T entity)
        {
        }

        public void Remove(T entity)
        {
        }
    }

    private sealed class NoOpOutbox : IOutboxWriter
    {
        public Task AddAsync(OutboxMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
