using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using BalanceService.Application.Commands;
using BalanceService.Domain.Events;
using BalanceService.Infrastructure.Messaging.Channel;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace BalanceService.Infrastructure.Messaging.Consumers
{
    public sealed class CreatedConsolidationConsumer : BackgroundService
    {
        private readonly ICreatedConsolidationConsumerChannel _consumerChannel;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CreatedConsolidationConsumer> _logger;

        public CreatedConsolidationConsumer(ICreatedConsolidationConsumerChannel consumerChannel, IServiceProvider serviceProvider, ILogger<CreatedConsolidationConsumer> logger)
        {
            _consumerChannel = consumerChannel;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            return base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var channel = await _consumerChannel.CreateChannelAsync();

            var consumer = new AsyncEventingBasicConsumer(channel);

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
                var (evt, deliveryTag) = tuple;

                if (stoppingToken.IsCancellationRequested) return;

                var commandHandler = await GetCommandHandler();

                await commandHandler.HandleAsync((CreateBalanceCommand)evt, stoppingToken);

                await channel.BasicAckAsync(deliveryTag, false);
            },

            ex => _logger.LogError(ex, "Error in consumer"),
            stoppingToken);

            await channel.BasicConsumeAsync("consolidation.created", autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
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
