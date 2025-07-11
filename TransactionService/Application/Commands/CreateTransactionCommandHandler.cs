using System.Text.Json;
using EventStore.Client;
using Grpc.Core;
using MongoDB.Driver;
using Polly;
using RabbitMQ.Client;
using TransactionService.Domain.Events;
using TransactionService.Infrastructure.Projections;
using TransactionService.Infrastructure.Utilities;

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
        var @event = (TransactionCreatedEvent)command;

        var accountId = @event.AccountId.ToString();

        var id = DeterministicId.For(accountId, @event.CreatedAt);

        var streamTransactionId = id.ToString();

        var eventId = Uuid.FromGuid(id);

        var streamName = $"transaction-{streamTransactionId}";
        var expectedVersion = StreamState.Any;

        var properties = new BasicProperties
        {
            Persistent = true
        };

        var eventData = new EventData(
            eventId,
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
                        _logger.LogWarning(ex, "RpcException, retry {RetryCount}", retryCount)
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
            var projection = new TransactionProjection(@event.TransactionId.ToString(), accountId, command.Amount, @event.CreatedAt);

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

            return id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process transaction {TransactionId}, sending to DLQ", streamTransactionId);

            // Send failed command to DLQ
            var dlqBody = JsonSerializer.SerializeToUtf8Bytes(new
            {
                FailedAt = DateTime.UtcNow,
                Command = command,
                Reason = ex.Message,
                @event.TransactionId
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


