using RabbitMQ.Client;

namespace ConsolicationService.Infrastructure.Messaging.Channels
{
    public interface ICreatedTransactionConsumerChannel : IConsolidationChannel;

    public class CreatedTransactionConsumerChannel : AsyncDisposableChannelBase, ICreatedTransactionConsumerChannel
    {
        private readonly IConnection _connection;

        public CreatedTransactionConsumerChannel(IConnection connection)
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
}
