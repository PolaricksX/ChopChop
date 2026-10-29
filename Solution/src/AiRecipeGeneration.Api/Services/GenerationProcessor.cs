using System.Net.Http;
using System.Text.Json;
using AiRecipeGeneration.Data;
using AiRecipeGeneration.Data.Providers;
using AiRecipeGeneration.Data.Repositories;
using AiRecipeGeneration.Domain;
using ChopChop.Shared;

namespace AiRecipeGeneration.Api.Services;

public sealed class GenerationProcessor(
    IUnitOfWork unitOfWork,
    IAiRecipeProvider provider,
    TimeProvider timeProvider,
    ILogger<GenerationProcessor> logger)
{
    public async Task ProcessAsync(
        Guid requestId,
        Guid ownerId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var request = await unitOfWork.CustomRepository<IGenerationQueries>()
            .GetOwnedAsync(requestId, ownerId, cancellationToken);
        if (request is null || request.Status is GenerationStatus.Completed or GenerationStatus.Failed or GenerationStatus.Confirmed)
            return;

        var now = timeProvider.GetUtcNow();
        if (request.Status == GenerationStatus.Queued)
            request.MarkProcessing(now);
        else if (!request.RestartExpiredProcessing(now, TimeSpan.FromMinutes(5)))
            return;

        unitOfWork.Repository<GenerationRequest>().Update(request);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            var draft = await provider.GenerateAsync(request, correlationId, cancellationToken);
            request.Complete(draft, timeProvider.GetUtcNow());
            await AddGenerationEventAsync(request, correlationId, "ai_recipe.generation.completed", cancellationToken);
        }
        catch (AiProviderOutputException exception)
        {
            logger.LogWarning(
                exception,
                "AI provider output was rejected for generation {GenerationId}.",
                request.Id);
            request.Fail("provider_output_invalid", timeProvider.GetUtcNow());
            await AddGenerationEventAsync(request, correlationId, "ai_recipe.generation.failed", cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "AI provider request failed for generation {GenerationId}.", request.Id);
            request.Fail("provider_unavailable", timeProvider.GetUtcNow());
            await AddGenerationEventAsync(request, correlationId, "ai_recipe.generation.failed", cancellationToken);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "AI provider timed out for generation {GenerationId}.", request.Id);
            request.Fail("provider_timeout", timeProvider.GetUtcNow());
            await AddGenerationEventAsync(request, correlationId, "ai_recipe.generation.failed", cancellationToken);
        }
        catch (AiProviderConfigurationException exception)
        {
            logger.LogError(exception, "AI provider configuration is invalid for generation {GenerationId}.", request.Id);
            request.Fail("provider_misconfigured", timeProvider.GetUtcNow());
            await AddGenerationEventAsync(request, correlationId, "ai_recipe.generation.failed", cancellationToken);
        }

        unitOfWork.Repository<GenerationRequest>().Update(request);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task AddGenerationEventAsync(
        GenerationRequest request,
        string correlationId,
        string eventName,
        CancellationToken cancellationToken)
    {
        var envelope = new EventEnvelope<GenerationCompleted>(
            Guid.NewGuid(),
            eventName,
            1,
            timeProvider.GetUtcNow(),
            "ai-recipe-generation",
            correlationId,
            new GenerationCompleted(request.Id, request.OwnerId, request.Status));
        var message = new OutboxMessage(
            envelope.EventId,
            envelope.EventType,
            eventName,
            JsonSerializer.Serialize(envelope, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            correlationId,
            envelope.OccurredAtUtc);
        await unitOfWork.CustomRepository<IOutboxWriter>().AddAsync(message, cancellationToken);
    }

    public sealed record GenerationCompleted(Guid RequestId, Guid OwnerId, GenerationStatus Status);
}
