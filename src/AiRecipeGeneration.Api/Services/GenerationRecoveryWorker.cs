using System.Text.Json;
using AiRecipeGeneration.Data;
using AiRecipeGeneration.Data.Repositories;
using AiRecipeGeneration.Domain;
using ChopChop.Shared;

namespace AiRecipeGeneration.Api.Services;

public sealed class GenerationRecoveryWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<GenerationRecoveryWorker> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions EventJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan ProcessingLease = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var recovered = await RecoverBatchAsync(stoppingToken);
                if (recovered == 0)
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Expired generation recovery failed; retrying.");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    private async Task<int> RecoverBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var now = timeProvider.GetUtcNow();
        var requests = await unitOfWork.CustomRepository<IGenerationQueries>()
            .GetExpiredProcessingAsync(now - ProcessingLease, 50, cancellationToken);

        foreach (var request in requests)
        {
            if (!request.RecoverExpiredProcessing(now))
                continue;

            var envelope = new EventEnvelope<GenerationApplicationService.GenerationRequested>(
                Guid.NewGuid(),
                "ai_recipe.generation.requested",
                1,
                now,
                "ai-recipe-generation",
                request.CorrelationId,
                new GenerationApplicationService.GenerationRequested(request.Id, request.OwnerId));
            var outboxMessage = new OutboxMessage(
                envelope.EventId,
                envelope.EventType,
                envelope.EventType,
                JsonSerializer.Serialize(envelope, EventJsonOptions),
                envelope.CorrelationId,
                envelope.OccurredAtUtc);
            unitOfWork.Repository<GenerationRequest>().Update(request);
            await unitOfWork.CustomRepository<IOutboxWriter>().AddAsync(outboxMessage, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return requests.Count;
    }
}
