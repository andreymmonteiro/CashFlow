using System.Text;
using System.Text.Json;
using EventStore.Client;
using TransactionService.Domain.Events;
using TransactionService.Infrastructure.EventHandlers;
using TransactionService.Infrastructure.Logging;
using TransactionService.Infrastructure.Messaging.Publishers;

namespace TransactionService.Infrastructure.EventStore;

public class EventStoreSubscriptionService : BackgroundService
{
    private readonly EventStoreClient _eventStore;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EventStoreSubscriptionService> _logger;

    public EventStoreSubscriptionService(
        EventStoreClient eventStore,
        IServiceProvider serviceProvider,
        ILogger<EventStoreSubscriptionService> logger)
    {
        _eventStore = eventStore;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _eventStore.SubscribeToAllAsync(
            FromAll.End,
            async (subscription, resolvedEvent, ct) =>
            {
                // Dynamically resolve handlers
                using var scope = _serviceProvider.CreateScope();

                try
                {
                    var eventType = resolvedEvent.Event.EventType;
                    var json = Encoding.UTF8.GetString(resolvedEvent.Event.Data.Span);

                    switch (eventType)
                    {
                        case nameof(TransactionCreatedEvent):
                            var @event = JsonSerializer.Deserialize<TransactionCreatedEvent>(json);

                            // Call Projection Handler
                            var projectionHandler = scope.ServiceProvider
                                .GetRequiredService<IEventHandler<TransactionCreatedEvent>>();
                            await projectionHandler.HandleAsync(@event, resolvedEvent.Event.EventStreamId, ct);

                            // Call Publisher Handler
                            var publisherHandler = scope.ServiceProvider
                                .GetRequiredService<IPublisherHandler<TransactionCreatedEvent>>();
                            await publisherHandler.HandleAsync(@event, ct);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing event {EventId}", resolvedEvent.Event.EventId);

                    var notifier = scope.ServiceProvider.GetRequiredService<IExceptionNotifier>();

                    var json = Encoding.UTF8.GetString(resolvedEvent.Event.Data.Span);

                    await notifier.Notify(ex, "transaction-dlq", json, stoppingToken);
                }
            },
            cancellationToken: stoppingToken);
    }
}

