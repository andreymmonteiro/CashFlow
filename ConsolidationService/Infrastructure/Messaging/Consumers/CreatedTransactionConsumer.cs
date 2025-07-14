using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using ConsolidationService.Application.Commands;
using ConsolidationService.Domain.Events;
using ConsolidationService.Infrastructure.Messaging.Channels;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ConsolidationService.Infrastructure.Messaging.Consumers
{
    public sealed class CreatedTransactionConsumer : BackgroundService
    {
        private readonly ICreatedTransactionConsumerChannel _channel;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CreatedTransactionConsumer> _logger;

        public CreatedTransactionConsumer(ICreatedTransactionConsumerChannel channel, IServiceProvider serviceProvider, ILogger<CreatedTransactionConsumer> logger)
        {
            _channel = channel;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            return base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var channel = await _channel.CreateChannelAsync();

            var consumer = new AsyncEventingBasicConsumer(channel);

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
                var (evt, deliveryTag) = tuple;

                if (stoppingToken.IsCancellationRequested) return;

                var commandHandler = await GetCommandHandler();

                await commandHandler.HandleAsync((CreateConsolidationCommand)evt, stoppingToken);

                await channel.BasicAckAsync(deliveryTag, false);
            },
            
            ex => _logger.LogError(ex, "Error in consumer"), 
            stoppingToken);

            await channel.BasicConsumeAsync("transaction.created", autoAck: false, consumer: consumer, stoppingToken);
        }

        private async Task<ICommandHandler<CreateConsolidationCommand, long>> GetCommandHandler() 
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            return scope.ServiceProvider
                .GetRequiredService<ICommandHandler<CreateConsolidationCommand, long>>();
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await base.StopAsync(cancellationToken);
        }
    }
}
