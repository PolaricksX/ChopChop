using AiRecipeGeneration.Data.Context;
using AiRecipeGeneration.Domain;
using Microsoft.EntityFrameworkCore;

namespace AiRecipeGeneration.Data.Repositories;

internal sealed class GenerationQueries(AiRecipeDbContext context) : IGenerationQueries
{
    public Task<GenerationRequest?> GetOwnedAsync(
        Guid requestId,
        Guid ownerId,
        CancellationToken cancellationToken) =>
        context.GenerationRequests.SingleOrDefaultAsync(
            request => request.Id == requestId && request.OwnerId == ownerId,
            cancellationToken);

    public Task<GenerationRequest?> GetByIdempotencyKeyAsync(
        Guid ownerId,
        string key,
        CancellationToken cancellationToken) =>
        context.GenerationRequests.SingleOrDefaultAsync(
            request => request.OwnerId == ownerId && request.IdempotencyKey == key,
            cancellationToken);

    public async Task<IReadOnlyList<GenerationRequest>> GetExpiredProcessingAsync(
        DateTimeOffset beforeUtc,
        int limit,
        CancellationToken cancellationToken) =>
        await context.GenerationRequests
            .Where(request =>
                request.Status == GenerationStatus.Processing &&
                request.ProcessingStartedAtUtc != null &&
                request.ProcessingStartedAtUtc <= beforeUtc)
            .OrderBy(request => request.ProcessingStartedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
}
