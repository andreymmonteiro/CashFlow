using RabbitMQ.Client;

namespace BalanceService.Infrastructure.Messaging.Channel;

public interface ICreatedBalancePublisherChannel : IBalanceChannel;

public class CreatedBalancePublisherChannel : AsyncDisposableChannelBase, ICreatedBalancePublisherChannel
{
    private readonly IConnection _connection;

    public CreatedBalancePublisherChannel(IConnection connection)
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

