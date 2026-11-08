using System.Text;
using ChopChop.Shared;
using Pantry.Data.Repositories;

namespace Pantry.Api.Services;

public sealed class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    IEventBus eventBus,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
                var messages = await store.GetUnpublishedAsync(20, stoppingToken);
                foreach (var message in messages)
                {
                    try
                    {
                        await eventBus.PublishSerializedAsync(ToRoutingKey(message.Type),
                            Encoding.UTF8.GetBytes(message.Payload), stoppingToken);
                        await store.MarkPublishedAsync(message.Id, DateTimeOffset.UtcNow, stoppingToken);
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        logger.LogError(exception, "Pantry outbox event {EventId} failed.", message.Id);
                    }
                }
                if (messages.Count == 0) await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                logger.LogError(exception, "Pantry outbox dispatch failed.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private static string ToRoutingKey(string type) =>
        "pantry.item." + type.Replace("PantryItem", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();
}
