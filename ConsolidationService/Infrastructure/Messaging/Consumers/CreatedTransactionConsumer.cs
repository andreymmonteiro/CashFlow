using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using ConsolidationService.Application.Commands;
using ConsolidationService.Domain.Aggregates;
using ConsolidationService.Domain.Events;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ConsolidationService.Infrastructure.Messaging.Consumers
{
    public sealed class CreatedTransactionConsumer : BackgroundService
    {
        private readonly IConnectionFactory _factory;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CreatedTransactionConsumer> _logger;

        private IConnection _connection;
        private IChannel _channel;
        private SemaphoreSlim _semaphore;

        private readonly int maxDegreeOfParallelism = 500; // Set the maximum degree of parallelism for processing messages

        public CreatedTransactionConsumer(IConnectionFactory factory, IServiceProvider serviceProvider, ILogger<CreatedTransactionConsumer> logger)
        {
            _factory = factory;
            _serviceProvider = serviceProvider;
            _logger = logger;
            _semaphore = new SemaphoreSlim(maxDegreeOfParallelism);
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
                    var evt = JsonSerializer.Deserialize<TransactionCreatedEvent>(json);
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

                                var command = Consolidation.CreateCommand(evt.AccountId, evt.Amount, evt.CreatedAt);

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

            await _channel.BasicConsumeAsync("transaction-created", autoAck: false, consumer: consumer, stoppingToken);
        }

        private async Task<ICommandHandler<CreateConsolidationCommand, long>> GetCommandHandler()
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            return scope.ServiceProvider
                .GetRequiredService<ICommandHandler<CreateConsolidationCommand, long>>();
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
    }
}
