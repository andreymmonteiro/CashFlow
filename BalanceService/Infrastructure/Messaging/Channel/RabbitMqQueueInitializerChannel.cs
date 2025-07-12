using RabbitMQ.Client;

namespace BalanceService.Infrastructure.Messaging.Channel;

public interface IRabbitMqQueueInitializerChannel : IBalanceChannel;

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

        return channel;

    }
}

