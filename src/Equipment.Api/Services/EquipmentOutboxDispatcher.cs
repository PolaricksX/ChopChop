using System.Text;
using ChopChop.Shared;
using Equipment.Data.Messaging;
using Equipment.Data.Persistence;
using Equipment.Data.Repositories;
using Microsoft.Extensions.Options;

namespace Equipment.Api.Services;

public sealed class EquipmentOutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    IEventBus eventBus,
    IOptions<EquipmentMessagingOptions> messagingOptions,
    TimeProvider clock,
    ILogger<EquipmentOutboxDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!messagingOptions.Value.Enabled)
        {
            logger.LogInformation("Equipment event dispatch is disabled; outbox messages remain pending.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await DispatchBatchAsync(stoppingToken);
                if (processed == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Equipment outbox batch could not be processed; retrying.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task<int> DispatchBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var messages = await scope.ServiceProvider.GetRequiredService<IEquipmentOutboxQueries>()
            .GetPendingAsync(20, clock.GetUtcNow(), cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                await eventBus.PublishSerializedAsync(
                    message.RoutingKey,
                    Encoding.UTF8.GetBytes(message.Payload),
                    cancellationToken);
                message.MarkPublished(clock.GetUtcNow());
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                message.MarkFailed(exception.GetType().Name, clock.GetUtcNow());
                logger.LogError(
                    exception,
                    "Equipment outbox event {OutboxEventId} failed on attempt {Attempt}.",
                    message.Id,
                    message.Attempts);
            }
        }

        if (messages.Count > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return messages.Count;
    }
}
