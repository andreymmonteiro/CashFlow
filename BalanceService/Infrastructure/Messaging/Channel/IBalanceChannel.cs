using RabbitMQ.Client;

namespace BalanceService.Infrastructure.Messaging.Channel
{
    public interface IBalanceChannel
    {
        Task<IChannel> CreateChannelAsync();
    }
}
