using BalanceService.Infrastructure.Messaging.Channel;

namespace BalanceService.Infrastructure.Messaging
{
    public sealed class RabbitMqQueueInitializer(IRabbitMqQueueInitializerChannel rabbitMqQueueInitializerChannel) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var channel = await rabbitMqQueueInitializerChannel.CreateChannelAsync();

            await channel.QueueDeclareAsync(
                queue: "consolidation.created",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);

            await channel.QueueDeclareAsync(
                queue: "balance.created",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);

            await channel.QueueDeclareAsync(
                queue: "balance.dlq",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
