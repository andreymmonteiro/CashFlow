using System.Text.Json;
using RabbitMQ.Client;
using StreamTail.Channels;

namespace ConsolidationService.Infrastructure.Logging;

public sealed class ExceptionNotifier : IExceptionNotifier
{
    private readonly IChannelPool _pool;
    private readonly ILogger<ExceptionNotifier> _logger;

    public ExceptionNotifier(IChannelPool pool, ILogger<ExceptionNotifier> logger)
    {
        _pool = pool;
        _logger = logger;
    }

    public async Task Notify(Exception exception, string dlqName, string message, CancellationToken cancellationToken)
    {
        var properties = new BasicProperties()
        {
            Persistent = true
        };

        _logger.LogError(exception, "Unhandled exception");

        _logger.LogError(exception, "Failed to process consolidation sending to DLQ");

        await using var lease = await _pool.RentAsync(cancellationToken);

        var body = JsonSerializer.SerializeToUtf8Bytes(new
        {
            FailedAt = DateTime.UtcNow,
            Reason = exception.Message,
            Content = message
        });

        await lease.Channel.BasicPublishAsync(
            exchange: "",
            routingKey: "",
            mandatory: true,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }
}
