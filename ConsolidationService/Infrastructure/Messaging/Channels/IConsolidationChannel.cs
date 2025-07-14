using RabbitMQ.Client;

namespace ConsolidationService.Infrastructure.Messaging.Channels
{
    public interface IConsolidationChannel
    {
        Task<IChannel> CreateChannelAsync();
    }
}
