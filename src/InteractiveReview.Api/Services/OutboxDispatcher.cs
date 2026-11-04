using System.Text;
using ChopChop.Shared;
using InteractiveReview.Data;
using InteractiveReview.Data.Repositories;

namespace InteractiveReview.Api.Services;

public sealed class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    IEventBus eventBus,
    TimeProvider timeProvider,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await DispatchBatchAsync(stoppingToken);
                if (processed == 0)
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Outbox batch could not be processed; retrying.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task<int> DispatchBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var messages = await unitOfWork.CustomRepository<IOutboxQueries>()
            .GetPendingAsync(20, timeProvider.GetUtcNow(), cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                await eventBus.PublishSerializedAsync(
                    message.RoutingKey,
                    Encoding.UTF8.GetBytes(message.Payload),
                    cancellationToken);
                message.MarkPublished(timeProvider.GetUtcNow());
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                message.MarkFailed(exception.GetType().Name, timeProvider.GetUtcNow());
                logger.LogError(
                    exception,
                    "Outbox event {OutboxEventId} failed on attempt {Attempt}.",
                    message.Id,
                    message.Attempts);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return messages.Count;
    }
}
