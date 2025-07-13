using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using TransactionService.Application.Commands;

namespace TransactionService.Infrastructure.Messaging.Consumers
{
    public sealed class CreateTransactionConsumer : BackgroundService
    {
        private readonly IConnection _connection;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CreateTransactionConsumer> _logger;

        public CreateTransactionConsumer( IConnection connection, IServiceProvider serviceProvider, ILogger<CreateTransactionConsumer> logger)
        {
            _connection = connection;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            return base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(channel);

            // Use Observable.FromEvent instead of Observable.FromEventPattern to handle the event subscription correctly
            var observable = Observable.FromEvent<AsyncEventHandler<BasicDeliverEventArgs>, BasicDeliverEventArgs>(
                handler => async (sender, args) => handler(args),
                h => consumer.ReceivedAsync += h,
                h => consumer.ReceivedAsync -= h)
                .Select(args =>
                {
                    var json = Encoding.UTF8.GetString(args.Body.ToArray());
                    var evt = JsonSerializer.Deserialize<CreateTransactionCommand>(json);
                    return (evt!, args.DeliveryTag);
                });

            observable.Subscribe(async tuple =>
            {
                var (command, deliveryTag) = tuple;

                if (stoppingToken.IsCancellationRequested) return;

                var commandHandler = await GetCommandHandler();

                await commandHandler.HandleAsync(command, stoppingToken);

                await channel.BasicAckAsync(deliveryTag, false);
            },

            ex => _logger.LogError(ex, "Error in consumer"),
            stoppingToken);

            await channel.BasicConsumeAsync("create.transaction", autoAck: false, consumer: consumer, stoppingToken);
        }

        private async Task<ICommandHandler<CreateTransactionCommand, Guid>> GetCommandHandler()
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            return scope.ServiceProvider
                .GetRequiredService<ICommandHandler<CreateTransactionCommand, Guid>>();
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await base.StopAsync(cancellationToken);
        }
    }
}
