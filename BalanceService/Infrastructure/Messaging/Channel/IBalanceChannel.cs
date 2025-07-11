using RabbitMQ.Client;

namespace BalanceService.Infrastructure.Messaging.Channel
{
    public interface IBalanceChannel
    {
        IChannel Channel { get; }

        Task InitializeChannelAsync();
    }
}
