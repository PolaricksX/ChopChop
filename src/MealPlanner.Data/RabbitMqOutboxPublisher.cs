using System.Text.Json;
using MealPlanner.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace MealPlanner.Data;

public sealed class RabbitMqOutboxPublisher(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqOutboxPublisher> logger) : BackgroundService
{
    private readonly RabbitMqOptions settings = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.Enabled)
        {
            return;
        }

        if (!Uri.TryCreate(settings.Uri, UriKind.Absolute, out var brokerUri)
            || brokerUri.Scheme is not ("amqp" or "amqps"))
        {
            logger.LogError("RabbitMQ publishing is enabled but RabbitMq:Uri is not a valid AMQP URI; outbox events remain pending.");
            return;
        }

        var factory = new ConnectionFactory { Uri = brokerUri };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await factory.CreateConnectionAsync(stoppingToken);
                var channelOptions = new CreateChannelOptions(true, true);
                await using var channel = await connection.CreateChannelAsync(channelOptions, stoppingToken);
                await channel.ExchangeDeclareAsync(
                    settings.Exchange,
                    ExchangeType.Topic,
                    durable: true,
                    cancellationToken: stoppingToken);
                await PublishPendingAsync(channel, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning("RabbitMQ outbox delivery failed ({ExceptionType}); pending events will be retried.", exception.GetType().Name);
            }

            await Task.Delay(TimeSpan.FromSeconds(settings.PollIntervalSeconds), stoppingToken);
        }
    }

    private async Task PublishPendingAsync(IChannel channel, CancellationToken token)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MealPlannerDbContext>();
        var messages = await context.OutboxMessages
            .Where(message => message.PublishedAt == null
                && message.DeadLetteredAt == null
                && message.NextAttemptAt <= DateTimeOffset.UtcNow)
            .OrderBy(message => message.CreatedAt)
            .Take(25)
            .ToListAsync(token);

        foreach (var message in messages)
        {
            try
            {
                var payload = JsonSerializer.Deserialize<JsonElement>(message.Payload);
                var envelope = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    eventId = message.Id,
                    eventType = message.EventType,
                    schemaVersion = message.SchemaVersion,
                    occurredAtUtc = message.OccurredAtUtc,
                    sourceService = message.SourceService,
                    correlationId = message.CorrelationId,
                    payload,
                });
                await channel.BasicPublishAsync(
                    settings.Exchange,
                    message.EventType,
                    mandatory: true,
                    basicProperties: new BasicProperties { MessageId = message.Id.ToString(), ContentType = "application/json" },
                    body: envelope,
                    cancellationToken: token);
                message.MarkPublished(DateTimeOffset.UtcNow);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                message.RecordFailure(exception.GetType().Name, DateTimeOffset.UtcNow);
                if (message.DeadLetteredAt is not null)
                {
                    logger.LogError(
                        "Outbox event {EventId} was dead-lettered after {Attempts} attempts ({ExceptionType}).",
                        message.Id,
                        message.AttemptCount,
                        message.LastError);
                }
            }
        }

        await context.SaveChangesAsync(token);
    }
}
