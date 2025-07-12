using RabbitMQ.Client;

namespace BalanceService.Infrastructure.Messaging.Channel;

public abstract class AsyncDisposableChannelBase : IAsyncDisposable
{
    private IChannel _channel;

    protected void SetChannel(IChannel channel)
    {
        _channel = channel;
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
            _channel = null;
        }

        GC.SuppressFinalize(this);
    }
}

