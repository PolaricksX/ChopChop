using System.Text.Json;
using ChopChop.Shared;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Equipment.Data.Messaging;

public sealed class EquipmentRabbitMqEventBus : IEventBus, IAsyncDisposable
{
    private const string Exchange = "chopchop.events";
    private const string Queue = "equipment.events";
    private readonly EquipmentMessagingOptions settings;
    private readonly SemaphoreSlim connectionLock = new(1, 1);
    private readonly ConnectionFactory? factory;
    private IConnection? connection;

    public EquipmentRabbitMqEventBus(IOptions<EquipmentMessagingOptions> options)
    {
        settings = options.Value;
        if (settings.Enabled)
        {
            factory = new ConnectionFactory { Uri = new Uri(settings.ConnectionString, UriKind.Absolute) };
        }
    }

    public Task PublishAsync<T>(
        string routingKey,
        EventEnvelope<T> message,
        CancellationToken cancellationToken) =>
        PublishSerializedAsync(
            routingKey,
            JsonSerializer.SerializeToUtf8Bytes(message),
            cancellationToken);

    public async Task PublishSerializedAsync(
        string routingKey,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        if (!settings.Enabled || factory is null)
        {
            throw new InvalidOperationException("Equipment event publishing is disabled.");
        }

        var activeConnection = await GetConnectionAsync(cancellationToken);
        await using var channel = await activeConnection.CreateChannelAsync(
            new CreateChannelOptions(true, true),
            cancellationToken);
        await channel.ExchangeDeclareAsync(
            Exchange,
            ExchangeType.Topic,
            durable: true,
            cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(
            Queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-message-ttl"] = 604_800_000,
                ["x-max-length"] = 100_000,
            },
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(Queue, Exchange, "equipment.#", cancellationToken: cancellationToken);
        await channel.BasicPublishAsync(
            Exchange,
            routingKey,
            mandatory: true,
            basicProperties: new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                MessageId = Guid.NewGuid().ToString("N"),
            },
            body: payload,
            cancellationToken: cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (connection is not null)
        {
            await connection.DisposeAsync();
        }

        connectionLock.Dispose();
    }

    private async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (connection is not null)
        {
            return connection;
        }

        await connectionLock.WaitAsync(cancellationToken);
        try
        {
            connection ??= await factory!.CreateConnectionAsync(cancellationToken);
            return connection;
        }
        finally
        {
            connectionLock.Release();
        }
    }
}
