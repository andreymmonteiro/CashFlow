using RabbitMQ.Client;

namespace ConsolicationService.Infrastructure.Messaging.Channels
{
    public interface IConsolidationChannel
    {
        IChannel Channel { get; }

        Task InitializeChannelAsync();
    }
}
