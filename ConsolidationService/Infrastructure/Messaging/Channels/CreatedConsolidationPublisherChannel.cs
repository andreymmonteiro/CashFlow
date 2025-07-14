using RabbitMQ.Client;

namespace ConsolidationService.Infrastructure.Messaging.Channels
{
    public interface ICreatedConsolidationPublisherChannel : IConsolidationChannel;

    public class CreatedConsolidationPublisherChannel : AsyncDisposableChannelBase, ICreatedConsolidationPublisherChannel
    {
        private readonly IConnection _connection;

        public CreatedConsolidationPublisherChannel(IConnection connection)
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
