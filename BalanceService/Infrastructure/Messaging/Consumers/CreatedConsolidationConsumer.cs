using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using BalanceService.Application.Commands;
using BalanceService.Domain.Aggregates;
using BalanceService.Domain.Events;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace BalanceService.Infrastructure.Messaging.Consumers
{
    public sealed class CreatedConsolidationConsumer : BackgroundService
    {
        private readonly IConnectionFactory _factory;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CreatedConsolidationConsumer> _logger;

        private IConnection _connection;
        private IChannel _channel;
        private SemaphoreSlim _semaphore;

        private readonly int maxDegreeOfParallelism = 500; // Set the maximum degree of parallelism for processing messages

        public CreatedConsolidationConsumer(IConnectionFactory factory, IServiceProvider serviceProvider, ILogger<CreatedConsolidationConsumer> logger)
        {
            _factory = factory;
            _serviceProvider = serviceProvider;
            _logger = logger;
            _semaphore = new SemaphoreSlim(maxDegreeOfParallelism);
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            return base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _connection = await _factory.CreateConnectionAsync(cancellationToken: stoppingToken);

            _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(_channel);

            // Use Observable.FromEvent instead of Observable.FromEventPattern to handle the event subscription correctly
            var observable = Observable.FromEvent<AsyncEventHandler<BasicDeliverEventArgs>, BasicDeliverEventArgs>(
                handler => async (sender, args) => handler(args),
                h => consumer.ReceivedAsync += h,
                h => consumer.ReceivedAsync -= h)
                .Select(args =>
                {
                    var json = Encoding.UTF8.GetString(args.Body.ToArray());
                    var evt = JsonSerializer.Deserialize<ConsolidationCreatedEvent>(json);
                    return (evt!, args.DeliveryTag);
                });

            observable.Subscribe(async tuple =>
            {
                await _semaphore.WaitAsync(stoppingToken);

                try
                {
                    var (evt, deliveryTag) = tuple;

                    if (stoppingToken.IsCancellationRequested) return;

                    var commandHandler = await GetCommandHandler();

                    var command = Balance.CreateCommand(evt.AccountId, evt.Debit, evt.Credit, evt.Date);

                    await commandHandler.HandleAsync(command, stoppingToken);

                    await _channel.BasicAckAsync(deliveryTag, false);
                }
                finally
                {
                    _semaphore.Release();
                }
            },

            ex => _logger.LogError(ex, "Error in consumer"),
            stoppingToken);

            await _channel.BasicConsumeAsync("consolidation.created", autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _channel?.DisposeAsync();
            _connection?.DisposeAsync();
            _semaphore?.Dispose();

            _channel = null;
            _connection = null;
            _semaphore = null;

            await base.StopAsync(cancellationToken);
        }

        private async Task<ICommandHandler<CreateBalanceCommand, long>> GetCommandHandler()
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            return scope.ServiceProvider
                .GetRequiredService<ICommandHandler<CreateBalanceCommand, long>>();
        }
    }
}
