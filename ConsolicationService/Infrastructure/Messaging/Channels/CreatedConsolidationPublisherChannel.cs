using RabbitMQ.Client;

namespace ConsolicationService.Infrastructure.Messaging.Channels
{
    public interface ICreatedConsolidationPublisherChannel : IConsolidationChannel;

    public class CreatedConsolidationPublisherChannel : ICreatedConsolidationPublisherChannel
    {
        private readonly IConnection _connection;

        public IChannel Channel { get; private set; }

        public CreatedConsolidationPublisherChannel(IConnection connection)
        {
            _connection = connection;
        }

        public async Task InitializeChannelAsync()
        {
            Channel = await _connection.CreateChannelAsync();

            await Channel.QueueDeclareAsync("transaction.created", durable: true, exclusive: false, autoDelete: false);
        }
    }
}
