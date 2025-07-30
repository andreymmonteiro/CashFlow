using System.Text;
using System.Text.Json;
using BalanceService.Domain.Events;
using BalanceService.Infrastructure.EventHandlers;
using BalanceService.Infrastructure.Messaging.Publishers;
using EventStore.Client;
using StreamTail.Logging;

namespace BalanceService.Infrastructure.EventStore;

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
                        case nameof(BalanceCreatedEvent):
                            var @event = JsonSerializer.Deserialize<BalanceCreatedEvent>(json);

                            // Call Projection Handler
                            var projectionHandler = scope.ServiceProvider
                                .GetRequiredService<IEventHandler<BalanceCreatedEvent>>();
                            await projectionHandler.HandleAsync(@event, resolvedEvent.Event.EventStreamId, ct);

                            // Call Publisher Handler
                            var publisherHandler = scope.ServiceProvider
                                .GetRequiredService<IPublisherHandler<BalanceCreatedEvent>>();
                            await publisherHandler.HandleAsync(@event, ct);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing event {EventId}", resolvedEvent.Event.EventId);

                    var notifier = scope.ServiceProvider.GetRequiredService<IExceptionNotifier>();

                    var json = Encoding.UTF8.GetString(resolvedEvent.Event.Data.Span);

                    await notifier.Notify(ex, "balance-dlq", json, stoppingToken);
                }
            },
            cancellationToken: stoppingToken);
    }
}

