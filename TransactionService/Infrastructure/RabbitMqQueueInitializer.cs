using RabbitMQ.Client;

namespace TransactionService.Infrastructure
{
    public sealed class RabbitMqQueueInitializer(IConnection connection) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(
                queue: "create-transaction",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);

            await channel.QueueDeclareAsync(
                queue: "transaction-created",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);

            await channel.QueueDeclareAsync(
                queue: "transaction-dlq",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
