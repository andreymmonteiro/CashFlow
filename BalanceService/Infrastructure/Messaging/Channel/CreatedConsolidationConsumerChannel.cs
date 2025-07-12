using RabbitMQ.Client;

namespace BalanceService.Infrastructure.Messaging.Channel;

public interface ICreatedConsolidationConsumerChannel : IBalanceChannel;

public class CreatedConsolidationConsumerChannel : AsyncDisposableChannelBase, ICreatedConsolidationConsumerChannel
{
    private readonly IConnection _connection;

    public CreatedConsolidationConsumerChannel(IConnection connection)
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
