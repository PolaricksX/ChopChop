using AiRecipeGeneration.Api.Services;
using AiRecipeGeneration.Data;
using AiRecipeGeneration.Data.Repositories;
using AiRecipeGeneration.Domain;

namespace AiRecipeGeneration.UnitTests;

public sealed class GenerationApplicationServiceTests
{
    [Fact]
    public async Task Another_user_cannot_read_or_confirm_a_generation()
    {
        var owner = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        var generation = GenerationRequest.Create(
            Guid.NewGuid(), owner, "request-1", "Create a quick dinner", false, [], "correlation-1", DateTimeOffset.UtcNow);
        var unitOfWork = new FakeUnitOfWork(generation);
        var service = new GenerationApplicationService(unitOfWork, TimeProvider.System);

        Assert.Null(await service.GetOwnedAsync(generation.Id, stranger, CancellationToken.None));
        Assert.False(await service.ConfirmAsync(generation.Id, stranger, "c", CancellationToken.None));

        Assert.Equal(0, unitOfWork.Saves);
        Assert.NotNull(await service.GetOwnedAsync(generation.Id, owner, CancellationToken.None));
    }

    private sealed class FakeUnitOfWork(GenerationRequest generation) : IUnitOfWork
    {
        public int Saves { get; private set; }

        public IRepository<T> Repository<T>() where T : class => throw new NotSupportedException();

        public T CustomRepository<T>() where T : class => typeof(T) == typeof(IGenerationQueries)
            ? (T)(object)new FakeQueries(generation)
            : throw new NotSupportedException(typeof(T).Name);

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(++Saves);
    }

    // Mirrors the real owner-filtered query so the test proves the service passes the caller's id.
    private sealed class FakeQueries(GenerationRequest generation) : IGenerationQueries
    {
        public Task<GenerationRequest?> GetOwnedAsync(Guid requestId, Guid ownerId, CancellationToken cancellationToken) =>
            Task.FromResult<GenerationRequest?>(generation.Id == requestId && generation.OwnerId == ownerId ? generation : null);

        public Task<GenerationRequest?> GetByIdempotencyKeyAsync(Guid ownerId, string key, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<GenerationRequest>> GetExpiredProcessingAsync(DateTimeOffset beforeUtc, int limit, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
