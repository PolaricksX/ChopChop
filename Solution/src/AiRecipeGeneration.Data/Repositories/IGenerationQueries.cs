using AiRecipeGeneration.Domain;

namespace AiRecipeGeneration.Data.Repositories;

public interface IGenerationQueries
{
    Task<GenerationRequest?> GetOwnedAsync(Guid requestId, Guid ownerId, CancellationToken cancellationToken);
    Task<GenerationRequest?> GetByIdempotencyKeyAsync(Guid ownerId, string key, CancellationToken cancellationToken);
    Task<IReadOnlyList<GenerationRequest>> GetExpiredProcessingAsync(
        DateTimeOffset beforeUtc,
        int limit,
        CancellationToken cancellationToken);
}
