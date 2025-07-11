using RabbitMQ.Client;

namespace BalanceService.Infrastructure.Messaging.Channel;

public interface ICreatedConsolidationConsumerChannel : IBalanceChannel;

public class CreatedConsolidationConsumerChannel : ICreatedConsolidationConsumerChannel
{
    private readonly IConnection _connection;

    public CreatedConsolidationConsumerChannel(IConnection connection)
    {
        _connection = connection;
    }

    public IChannel Channel { get; private set; }

    public async Task InitializeChannelAsync()
    {
        Channel = await _connection.CreateChannelAsync();

        await Channel.QueueDeclareAsync("transaction.created", durable: true, exclusive: false, autoDelete: false);
    }
}
