using RabbitMQ.Client;

namespace ConsolidationService.Infrastructure.Messaging.Channels;

public interface IRabbitMqQueueInitializerChannel : IConsolidationChannel;

public class RabbitMqQueueInitializerChannel : AsyncDisposableChannelBase, IRabbitMqQueueInitializerChannel
{
    private readonly IConnection _connection;

    public RabbitMqQueueInitializerChannel(IConnection connection)
    {
        _connection = connection;
    }

    public async Task<IChannel> CreateChannelAsync()
    {
        var channel = await _connection.CreateChannelAsync();

        await channel.QueueDeclareAsync("transaction.created", durable: true, exclusive: false, autoDelete: false);

        SetChannel(channel);

        return channel;

    }
}

