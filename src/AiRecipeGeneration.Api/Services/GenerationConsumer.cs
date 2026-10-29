using System.Text.Json;
using AiRecipeGeneration.Api.Services;
using ChopChop.Shared;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace AiRecipeGeneration.Api.Services;

public sealed class GenerationConsumer(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<GenerationConsumer> logger) : BackgroundService
{
    private const string Exchange = "chopchop.events";
    private const string Queue = "ai_recipe.generation.requested";
    private const string DeadLetterExchange = "chopchop.events.dead";
    private const string DeadLetterQueue = "ai_recipe.generation.dead";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionString = configuration["Messaging:ConnectionString"]
            ?? throw new InvalidOperationException("Messaging:ConnectionString is required.");
        var factory = new ConnectionFactory { Uri = new Uri(connectionString) };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(factory, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Generation queue connection failed; reconnecting.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ConsumeAsync(ConnectionFactory factory, CancellationToken stoppingToken)
    {
        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await channel.ExchangeDeclareAsync(
            Exchange,
            ExchangeType.Topic,
            durable: true,
            cancellationToken: stoppingToken);
        await channel.ExchangeDeclareAsync(
            DeadLetterExchange,
            ExchangeType.Direct,
            durable: true,
            cancellationToken: stoppingToken);
        await channel.QueueDeclareAsync(
            DeadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);
        await channel.QueueBindAsync(DeadLetterQueue, DeadLetterExchange, DeadLetterQueue, cancellationToken: stoppingToken);
        await channel.QueueDeclareAsync(
            Queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = DeadLetterExchange,
                ["x-dead-letter-routing-key"] = DeadLetterQueue
            },
            cancellationToken: stoppingToken);
        await channel.QueueBindAsync(Queue, Exchange, Queue, cancellationToken: stoppingToken);
        await channel.BasicQosAsync(0, 1, false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            var body = delivery.Body.ToArray();
            try
            {
                var envelope = JsonSerializer.Deserialize<
                    EventEnvelope<GenerationApplicationService.GenerationRequested>>(body, JsonOptions);
                if (envelope is null || envelope.Payload.RequestId == Guid.Empty || envelope.Payload.OwnerId == Guid.Empty)
                    throw new JsonException("Generation event is missing its required identifiers.");

                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<GenerationProcessor>();
                await processor.ProcessAsync(
                    envelope.Payload.RequestId,
                    envelope.Payload.OwnerId,
                    envelope.CorrelationId,
                    stoppingToken);
                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, stoppingToken);
            }
            catch (JsonException exception)
            {
                logger.LogError(exception, "Malformed generation event was routed to the dead-letter queue.");
                await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Generation event processing failed; message will be requeued.");
                await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true, stoppingToken);
            }
        };

        await channel.BasicConsumeAsync(Queue, autoAck: false, consumer, stoppingToken);
        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }
}
