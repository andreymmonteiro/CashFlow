using RabbitMQ.Client;

namespace ConsolicationService.Infrastructure.Messaging.Channels
{
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
}
