using RabbitMQ.Client;

namespace ConsolicationService.Infrastructure.Messaging.Channels
{
    public interface IConsolidationChannel
    {
        Task<IChannel> CreateChannelAsync();
    }
}
