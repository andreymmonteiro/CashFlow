using System.Text.Json;
using ConsolidationService.Domain.Events;
using RabbitMQ.Client;
using StreamTail.Channels;

namespace ConsolidationService.Infrastructure.Messaging.Publishers;

public class ConsolidationCreatedPublisherHandler : IPublisherHandler<ConsolidationCreatedEvent>
{
    private readonly IChannelPool _pool;
    private readonly ILogger<ConsolidationCreatedPublisherHandler> _logger;

    public ConsolidationCreatedPublisherHandler(IChannelPool pool, ILogger<ConsolidationCreatedPublisherHandler> logger)
    {
        _pool = pool;
        _logger = logger;
    }

    public async Task HandleAsync(ConsolidationCreatedEvent @event, CancellationToken cancellationToken)
    {
        await using var lease = await _pool.RentAsync(cancellationToken);

        var body = JsonSerializer.SerializeToUtf8Bytes(@event);

        var properties = new BasicProperties
        {
            Persistent = true
        };
        
        var channel = lease.Channel;

        await channel.QueueDeclareAsync("consolidation-created", durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);

        await channel.BasicPublishAsync(
            exchange: "",
            routingKey: "consolidation-created",
            mandatory: true,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Published ConsolidationCreatedEvent to RabbitMQ");
    }
}