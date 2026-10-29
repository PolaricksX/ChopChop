using System.Text;
using System.Text.Json;
using ChopChop.Shared;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace AiRecipeGeneration.Data.Messaging;

public sealed class RabbitMqEventBus : IEventBus, IAsyncDisposable
{
    private const string Exchange = "chopchop.events";
    private const string EventQueue = "ai_recipe.events";
    private readonly ConnectionFactory _factory;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnection? _connection;

    public RabbitMqEventBus(IConfiguration configuration)
    {
        var connectionString = configuration["Messaging:ConnectionString"]
            ?? throw new InvalidOperationException("Messaging:ConnectionString is required.");
        _factory = new ConnectionFactory { Uri = new Uri(connectionString) };
    }

    public Task PublishAsync<T>(
        string routingKey,
        EventEnvelope<T> message,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(message);
        return PublishSerializedAsync(routingKey, payload, cancellationToken);
    }

    public async Task PublishSerializedAsync(
        string routingKey,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(true, true),
            cancellationToken);
        await channel.ExchangeDeclareAsync(
            Exchange,
            ExchangeType.Topic,
            durable: true,
            cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(
            EventQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-message-ttl"] = 604_800_000,
                ["x-max-length"] = 100_000
            },
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(EventQueue, Exchange, "ai_recipe.#", cancellationToken: cancellationToken);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = Guid.NewGuid().ToString("N")
        };
        await channel.BasicPublishAsync(
            Exchange,
            routingKey,
            mandatory: true,
            basicProperties: properties,
            body: payload,
            cancellationToken: cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
        _connectionLock.Dispose();
    }

    private async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
            return _connection;

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            _connection ??= await _factory.CreateConnectionAsync(cancellationToken);
            return _connection;
        }
        finally
        {
            _connectionLock.Release();
        }
    }
}
