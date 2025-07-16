using RabbitMQ.Client;

namespace ConsolidationService.Infrastructure.Messaging.Channels;

public interface IChannelPool : IAsyncDisposable
{
    ValueTask<ChannelLease> RentAsync(CancellationToken ct = default);

    Task Return(IChannel channel);
}

