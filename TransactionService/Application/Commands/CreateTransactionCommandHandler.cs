using System.Text.Json;
using EventStore.Client;
using Grpc.Core;
using MongoDB.Driver;
using Polly;
using RabbitMQ.Client;
using TransactionService.Domain.Events;
using TransactionService.Infrastructure.Projections;

namespace TransactionService.Application.Commands;

public sealed class CreateTransactionCommandHandler : ICommandHandler<CreateTransactionCommand, Guid>
{
    private readonly EventStoreClient _eventStore;
    private readonly IMongoCollection<TransactionProjection> _transaction;
    private readonly IChannel _channel;
    private readonly ILogger<CreateTransactionCommandHandler> _logger;

    public CreateTransactionCommandHandler(
        EventStoreClient eventStore,
        IMongoCollection<TransactionProjection> mongoCollection,
        IChannel channel,
        ILogger<CreateTransactionCommandHandler> logger)
    {
        _eventStore = eventStore;
        _transaction = mongoCollection;
        _channel = channel;
        _logger = logger;
    }

    public async Task<Guid> HandleAsync(CreateTransactionCommand command, CancellationToken cancellationToken)
    {
        var transactionId = Guid.NewGuid();
        var streamName = $"transaction-{transactionId}";
        var expectedVersion = StreamState.NoStream;

        var @event = new TransactionCreatedEvent(transactionId, command.AccountId, command.Amount, DateTime.UtcNow);

        var properties = new BasicProperties
        {
            Persistent = true
        };

        var eventData = new EventData(
            Uuid.NewUuid(),
            nameof(TransactionCreatedEvent),
            JsonSerializer.SerializeToUtf8Bytes(@event));

        try
        {
            // Retry EventStore append on RpcException
            await Policy
                .Handle<RpcException>(ex =>
                    ex.StatusCode == StatusCode.Unavailable
                 || ex.StatusCode == StatusCode.ResourceExhausted
                 || ex.StatusCode == StatusCode.DeadlineExceeded)
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(100 * attempt),
                    onRetry: (ex, ts, retryCount, _) =>
                        _logger.LogWarning(ex, "Concurrency conflict on EventStore write, retry {RetryCount}", retryCount)
                )
                .ExecuteAsync(async () =>
                {
                    await _eventStore.AppendToStreamAsync(
                        streamName,
                        expectedVersion,
                        new[] { eventData },
                        cancellationToken: cancellationToken);
                });

            // Idempotent projection write
            var projection = new TransactionProjection(transactionId.ToString(), command.AccountId.ToString(), command.Amount, @event.CreatedAt);

            var replaceResult = await _transaction.ReplaceOneAsync(
                filter: x => x.TransactionId == projection.TransactionId,
                replacement: projection,
                options: new ReplaceOptions { IsUpsert = true },
                cancellationToken);

            _logger.LogInformation("MongoDB upsert result: {Result}", replaceResult);

            // Publish event
            var body = JsonSerializer.SerializeToUtf8Bytes(@event);

            await _channel.BasicPublishAsync(
                exchange: "",
                routingKey: "transaction.created",
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Published TransactionCreatedEvent to RabbitMQ");

            return transactionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process transaction {TransactionId}, sending to DLQ", transactionId);

            // Send failed command to DLQ
            var dlqBody = JsonSerializer.SerializeToUtf8Bytes(new
            {
                FailedAt = DateTime.UtcNow,
                Command = command,
                Reason = ex.Message,
                TransactionId = transactionId
            });

            await _channel.BasicPublishAsync(
                exchange: "",
                routingKey: "transaction.dlq",
                mandatory: true,
                basicProperties: properties,
                body: dlqBody);

            throw;
        }
    }
}


