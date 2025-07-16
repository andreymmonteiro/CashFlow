using RabbitMQ.Client;

namespace BalanceService.Infrastructure.Messaging.Channels;

public sealed class ChannelLease : IAsyncDisposable
{
    private readonly IChannelPool _pool;
    internal readonly IChannel Channel;

    public ChannelLease(IChannelPool pool, IChannel channel)
    {
        _pool = pool;
        Channel = channel;
    }

    public async ValueTask DisposeAsync()
    {
        // give the channel back to the pool
        await _pool.Return(Channel);
    }
}

