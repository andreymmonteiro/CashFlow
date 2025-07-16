using System.Text.Json;
using EventStore.Client;
using Grpc.Core;
using MongoDB.Driver;
using Polly;
using RabbitMQ.Client;
using TransactionService.Domain.Aggregates;
using TransactionService.Domain.Events;
using TransactionService.Infrastructure.EventStore;
using TransactionService.Infrastructure.Messaging.Channels;
using TransactionService.Infrastructure.Projections;
using TransactionService.Infrastructure.Utilities;

namespace TransactionService.Application.Commands;

public sealed class CreateTransactionCommandHandler : ICommandHandler<CreateTransactionCommand, Guid>
{
    private readonly IEventStoreWrapper _eventStore;
    private readonly IChannelPool _pool;
    private readonly IMongoCollection<TransactionProjection> _transaction;
    private readonly ILogger<CreateTransactionCommandHandler> _logger;

    public CreateTransactionCommandHandler(
        IEventStoreWrapper eventStore,
        IChannelPool pool,
        IMongoCollection<TransactionProjection> mongoCollection,
        ILogger<CreateTransactionCommandHandler> logger
        )
    {
        _eventStore = eventStore;
        _pool = pool;
        _transaction = mongoCollection;
        _logger = logger;
        _pool = pool;
    }

    public async Task<Guid> HandleAsync(CreateTransactionCommand command, CancellationToken cancellationToken)
    {
        await using var lease = await _pool.RentAsync(cancellationToken);

        var transaction = Transaction.Create(
            command.AccountId,
            command.Amount);

        var @event = transaction.ToCreatedEvent();

        var accountId = transaction.AccountId.ToString();

        var transactionId = transaction.TransactionId.ToString();

        var id = DeterministicId.For(accountId, transaction.CreatedAt);

        var streamId = id.ToString();

        var eventId = Uuid.FromGuid(id);

        var streamName = $"transaction-{streamId}";
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
            _logger.LogInformation("Handling CreateTransactionCommand for AccountId: {AccountId}, Amount: {Amount}, TransactionId: {TransactionId}",
                                command.AccountId, command.Amount, transactionId);

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
            var projection = new TransactionProjection(transaction.TransactionId.ToString(), accountId, transaction.Amount, transaction.CreatedAt);

            var filter = Builders<TransactionProjection>.Filter.And(
                Builders<TransactionProjection>.Filter.Eq(c => c.AccountId, accountId),
                Builders<TransactionProjection>.Filter.Eq(c => c.TransactionId, transactionId),
                Builders<TransactionProjection>.Filter.Eq(c => c.CreatedAt, @event.CreatedAt),
                Builders<TransactionProjection>.Filter.Eq(c => c.AppliedStreamId, streamId)
                );

            var update = Builders<TransactionProjection>.Update
                 .SetOnInsert(c => c.AccountId, accountId)
                 .SetOnInsert(c => c.TransactionId, transactionId)
                 .SetOnInsert(c => c.CreatedAt, @event.CreatedAt)
                 .SetOnInsert(c => c.Amount, @event.Amount)
                 .SetOnInsert(c => c.AppliedStreamId, streamId);

            var options = new UpdateOptions { IsUpsert = true };

            var updateResult = await _transaction.UpdateOneAsync(
                    filter: filter,
                    update: update,
                    options: options,
                    cancellationToken);

            _logger.LogInformation("MongoDB upsert result: {Result}", updateResult);

            // Publish event
            var body = JsonSerializer.SerializeToUtf8Bytes(@event);

            await lease.Channel.BasicPublishAsync(
                exchange: "",
                routingKey: "transaction-created",
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Published TransactionCreatedEvent to RabbitMQ");

            return id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process transaction {TransactionId}, sending to DLQ", streamId);

            // Send failed command to DLQ
            
            var dlqBody = JsonSerializer.SerializeToUtf8Bytes(new
            {
                FailedAt = DateTime.UtcNow,
                Command = command,
                Reason = ex.Message,
                TransactionId = transactionId
            });

            await lease.Channel.BasicPublishAsync(
                exchange: "",
                routingKey: "transaction-dlq",
                mandatory: true,
                basicProperties: properties,
                body: dlqBody);

            throw;
        }
    }
}


