using System.Threading.Channels;
using ConsolicationService.Infrastructure.Messaging.Channels;

namespace ConsolicationService.Infrastructure.Messaging
{
    public sealed class RabbitMqQueueInitializer(IRabbitMqQueueInitializerChannel rabbitMqQueueInitializerChannel) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var channel = rabbitMqQueueInitializerChannel.Channel;

            await channel.QueueDeclareAsync(
                queue: "transaction.created",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);

            await channel.QueueDeclareAsync(
                queue: "consolidation.created",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);

            await channel.QueueDeclareAsync(
                queue: "consolidation.dlq",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
