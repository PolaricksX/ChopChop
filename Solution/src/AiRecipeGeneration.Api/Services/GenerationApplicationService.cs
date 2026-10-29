using System.Text.Json;
using AiRecipeGeneration.Api.Contracts;
using AiRecipeGeneration.Data;
using AiRecipeGeneration.Data.Repositories;
using AiRecipeGeneration.Domain;
using ChopChop.Shared;

namespace AiRecipeGeneration.Api.Services;

public sealed class GenerationApplicationService(
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions EventJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<GenerationResponse> CreateAsync(
        Guid ownerId,
        string idempotencyKey,
        CreateGenerationRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var queries = unitOfWork.CustomRepository<IGenerationQueries>();
        var existing = await queries.GetByIdempotencyKeyAsync(ownerId, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.Prompt != request.Prompt.Trim() ||
                existing.IncludePantry != request.IncludePantry ||
                !existing.PantryItems.SequenceEqual(request.IncludePantry ? request.PantryItems ?? [] : []))
            {
                throw new IdempotencyConflictException();
            }

            return ToResponse(existing);
        }

        var generation = GenerationRequest.Create(
            Guid.NewGuid(),
            ownerId,
            idempotencyKey,
            request.Prompt,
            request.IncludePantry,
            request.PantryItems,
            correlationId,
            timeProvider.GetUtcNow());

        var eventPayload = new GenerationRequested(generation.Id, ownerId);
        var envelope = new EventEnvelope<GenerationRequested>(
            Guid.NewGuid(),
            "ai_recipe.generation.requested",
            1,
            timeProvider.GetUtcNow(),
            "ai-recipe-generation",
            correlationId,
            eventPayload);
        var outboxMessage = new OutboxMessage(
            envelope.EventId,
            envelope.EventType,
            "ai_recipe.generation.requested",
            JsonSerializer.Serialize(envelope, EventJsonOptions),
            correlationId,
            envelope.OccurredAtUtc);

        await unitOfWork.Repository<GenerationRequest>().AddAsync(generation, cancellationToken);
        await unitOfWork.CustomRepository<IOutboxWriter>().AddAsync(outboxMessage, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(generation);
    }

    public async Task<GenerationResponse?> GetOwnedAsync(
        Guid requestId,
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        var generation = await unitOfWork.CustomRepository<IGenerationQueries>()
            .GetOwnedAsync(requestId, ownerId, cancellationToken);
        return generation is null ? null : ToResponse(generation);
    }

    public async Task<bool> ConfirmAsync(
        Guid requestId,
        Guid ownerId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var generation = await unitOfWork.CustomRepository<IGenerationQueries>()
            .GetOwnedAsync(requestId, ownerId, cancellationToken);
        if (generation is null)
            return false;

        generation.Confirm(ownerId);
        unitOfWork.Repository<GenerationRequest>().Update(generation);
        var envelope = new EventEnvelope<GenerationConfirmed>(
            Guid.NewGuid(),
            "ai_recipe.generation.confirmed",
            1,
            timeProvider.GetUtcNow(),
            "ai-recipe-generation",
            correlationId,
            new GenerationConfirmed(generation.Id, generation.OwnerId));
        var message = new OutboxMessage(
            envelope.EventId,
            envelope.EventType,
            envelope.EventType,
            JsonSerializer.Serialize(envelope, EventJsonOptions),
            correlationId,
            envelope.OccurredAtUtc);
        await unitOfWork.CustomRepository<IOutboxWriter>().AddAsync(message, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static GenerationResponse ToResponse(GenerationRequest request) =>
        new(request.Id, request.Status, request.CreatedAtUtc, request.CompletedAtUtc, request.Draft, request.FailureCode);

    public sealed record GenerationRequested(Guid RequestId, Guid OwnerId);
    public sealed record GenerationConfirmed(Guid RequestId, Guid OwnerId);
}

public sealed class IdempotencyConflictException : Exception
{
}
