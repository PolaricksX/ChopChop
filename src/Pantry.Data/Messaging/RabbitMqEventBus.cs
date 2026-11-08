using System.Text.Json;
using ChopChop.Shared;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace Pantry.Data.Messaging;

public sealed class RabbitMqEventBus(IConfiguration configuration) : IEventBus, IAsyncDisposable
{
    private const string Exchange = "chopchop.events";
    private readonly ConnectionFactory factory = new()
    {
        Uri = new Uri(configuration["Messaging:ConnectionString"]
            ?? throw new InvalidOperationException("Messaging:ConnectionString is required."))
    };
    private readonly SemaphoreSlim connectionLock = new(1, 1);
    private IConnection? connection;

    public Task PublishAsync<T>(string routingKey, EventEnvelope<T> message, CancellationToken cancellationToken) =>
        PublishSerializedAsync(routingKey, JsonSerializer.SerializeToUtf8Bytes(message), cancellationToken);

    public async Task PublishSerializedAsync(string routingKey, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        var broker = await GetConnectionAsync(cancellationToken);
        await using var channel = await broker.CreateChannelAsync(cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true, cancellationToken: cancellationToken);
        await channel.BasicPublishAsync(Exchange, routingKey, mandatory: true,
            basicProperties: new BasicProperties { Persistent = true, ContentType = "application/json" },
            body: payload, cancellationToken: cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (connection is not null) await connection.DisposeAsync();
        connectionLock.Dispose();
    }

    private async Task<IConnection> GetConnectionAsync(CancellationToken token)
    {
        if (connection is not null) return connection;
        await connectionLock.WaitAsync(token);
        try { return connection ??= await factory.CreateConnectionAsync(token); }
        finally { connectionLock.Release(); }
    }
}
