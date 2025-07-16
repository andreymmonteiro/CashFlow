using RabbitMQ.Client;

namespace BalanceService.Infrastructure.Messaging.Channels;

public interface IChannelPool : IAsyncDisposable
{
    ValueTask<ChannelLease> RentAsync(CancellationToken ct = default);

    Task Return(IChannel channel);
}

