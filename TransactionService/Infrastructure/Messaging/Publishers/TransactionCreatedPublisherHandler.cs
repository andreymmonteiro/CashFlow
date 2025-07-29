using System.Text.Json;
using RabbitMQ.Client;
using TransactionService.Domain.Events;
using TransactionService.Infrastructure.Messaging.Channels;

namespace TransactionService.Infrastructure.Messaging.Publishers;

public sealed class TransactionCreatedPublisherHandler : IPublisherHandler<TransactionCreatedEvent>
{
    private readonly IChannelPool _pool;
    private readonly ILogger<TransactionCreatedPublisherHandler> _logger;

    public TransactionCreatedPublisherHandler(IChannelPool pool, ILogger<TransactionCreatedPublisherHandler> logger)
    {
        _pool = pool;
        _logger = logger;
    }

    public async Task HandleAsync(TransactionCreatedEvent @event, CancellationToken cancellationToken)
    {
        await using var lease = await _pool.RentAsync(cancellationToken);
        
        var body = JsonSerializer.SerializeToUtf8Bytes(@event);

        var properties = new BasicProperties
        {
            Persistent = true
        };

        await lease.Channel.BasicPublishAsync(
            exchange: "",
            routingKey: "transaction-created",
            mandatory: true,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Published TransactionCreatedEvent to RabbitMQ");
    }
}
