using RabbitMQ.Client;

namespace TransactionService.Infrastructure
{
    public sealed class RabbitMqQueueInitializer(IChannel Channel) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await Channel.QueueDeclareAsync(
                queue: "transaction.created",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);

            await Channel.QueueDeclareAsync(
                queue: "transaction.dlq",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
