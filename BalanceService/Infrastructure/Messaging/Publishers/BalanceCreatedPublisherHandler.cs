using System.Text.Json;
using BalanceService.Domain.Events;
using RabbitMQ.Client;
using StreamTail.Channels;

namespace BalanceService.Infrastructure.Messaging.Publishers
{
    public class BalanceCreatedPublisherHandler : IPublisherHandler<BalanceCreatedEvent>
    {
        private readonly IChannelPool _channelPool;
        private readonly ILogger<BalanceCreatedPublisherHandler> _logger;

        public BalanceCreatedPublisherHandler(IChannelPool channelPool, ILogger<BalanceCreatedPublisherHandler> logger)
        {
            _channelPool = channelPool;
            _logger = logger;
        }

        public async Task HandleAsync(BalanceCreatedEvent @event, CancellationToken cancellationToken)
        {
            await using var lease = await _channelPool.RentAsync(cancellationToken);
            var channel = lease.Channel;

            var properties = new BasicProperties
            {
                Persistent = true
            };

            var body = JsonSerializer.SerializeToUtf8Bytes(@event);

            await channel.QueueDeclareAsync("balance-created", durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);

            await channel.BasicPublishAsync(
                exchange: "",
                routingKey: "balance-created",
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Published BalanceCreatedEvent to RabbitMQ");
        }
    }
}
