using System.Text.Json;
using BalanceService.Domain.Aggregates;
using BalanceService.Domain.Events;
using BalanceService.Infrastructure.EventStore;
using BalanceService.Infrastructure.Projections;
using BalanceService.Infrastructure.Utilities;
using EventStore.Client;
using Grpc.Core;
using MongoDB.Driver;
using Polly;
using RabbitMQ.Client;

namespace BalanceService.Application.Commands
{
    public class CreateBalanceCommandHandler : ICommandHandler<CreateBalanceCommand, long>
    {
        private readonly IEventStoreWrapper _eventStore;
        private readonly IConnection _connection;
        private readonly IMongoCollection<BalanceProjection> _balances;
        private readonly ILogger<CreateBalanceCommandHandler> _logger;

        public CreateBalanceCommandHandler(IEventStoreWrapper eventStore, IConnection connection, IMongoCollection<BalanceProjection> balances, ILogger<CreateBalanceCommandHandler> logger)
        {
            _connection = connection;
            _logger = logger;
            _balances = balances;
            _eventStore = eventStore;
        }

        public async Task<long> HandleAsync(CreateBalanceCommand command, CancellationToken cancellationToken)
        {
            using var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

            var properties = new BasicProperties
            {
                Persistent = true
            };

            var accountId = command.AccountId;

            var balance = Balance.Create(command.AccountId, command.Amount);

            var @event = balance.ToCreatedEvent();

            var date = command.Date;

            var id = DeterministicId.For(command.AccountId, date);

            var eventId = Uuid.FromGuid(id);

            var streamId = id.ToString();

            try
            {
                _logger.LogInformation("Handling CreateBalanceCommand for AccountId: {AccountId}, Amount: {Amount}",
                                    command.AccountId, command.Amount);

                await InsertEventAsync(@event, streamId, eventId, cancellationToken);

                var modifiedCount = await UpsertProjectionAsync(balance, accountId, streamId, cancellationToken);

                var body = JsonSerializer.SerializeToUtf8Bytes(@event);

                await channel.BasicPublishAsync(
                    exchange: "",
                    routingKey: "balance.created",
                    mandatory: true,
                    basicProperties: properties,
                    body: body,
                    cancellationToken: cancellationToken);

                _logger.LogInformation("Published BalanceCreatedEvent to RabbitMQ");

                return modifiedCount;
            }
            catch(Exception e)
            {
                _logger.LogError(e, "Error handling CreateBalanceCommand for AccountId: {AccountId}", command.AccountId);

                // Send failed command to DLQ
                var dlqBody = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    FailedAt = DateTime.UtcNow,
                    Command = command,
                    Reason = e.Message,
                    ConsolidationId = streamId
                });

                await channel.BasicPublishAsync(
                    exchange: "",
                    routingKey: "consolidation.dlq",
                    mandatory: true,
                    basicProperties: properties,
                    body: dlqBody);

                return 0;
            }
        }

        private async Task<long> UpsertProjectionAsync(Balance balance, string accountId, string streamId, CancellationToken cancellationToken)
        {
            var filter = Builders<BalanceProjection>.Filter.And(
                Builders<BalanceProjection>.Filter.Eq(c => c.AccountId, accountId),
                Builders<BalanceProjection>.Filter.Not(
                    Builders<BalanceProjection>.Filter.AnyEq(c => c.AppliedStreamIds, streamId))
            );

            var update = Builders<BalanceProjection>.Update
                 .SetOnInsert(c => c.AccountId, accountId)
                 .Inc(c => c.Amount, balance.Amount)
                 .Push(c => c.AppliedStreamIds, streamId);

            var options = new UpdateOptions { IsUpsert = false };
            var upsertResult = await _balances.UpdateOneAsync(filter, update, options, cancellationToken);

            if (upsertResult.ModifiedCount == 0)
            {
                var documentFilter = Builders<BalanceProjection>.Filter.And(
                    Builders<BalanceProjection>.Filter.Eq(c => c.AccountId, accountId));

                var count = await _balances.CountDocumentsAsync(documentFilter, cancellationToken: cancellationToken);

                if(count > 0)
                {
                    return 0;
                }

                var insertOptions = new InsertOneOptions()
                {
                    BypassDocumentValidation = true
                };

                var projection = new BalanceProjection
                {
                    AccountId = accountId,
                    Amount = balance.Amount,
                    AppliedStreamIds = new List<string> { streamId }
                };

                await _balances.InsertOneAsync(projection, insertOptions, cancellationToken);

                return 1;
            }

            return upsertResult.ModifiedCount;
        }

        private async Task InsertEventAsync(BalanceCreatedEvent @event, string balanceId, Uuid eventId, CancellationToken cancellationToken)
        {
            var streamName = $"balance-{balanceId}";
            var expectedVersion = StreamState.Any;

            var eventData = new EventData(
                eventId,
                nameof(BalanceCreatedEvent),
                JsonSerializer.SerializeToUtf8Bytes(@event));

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
        }
    }
}
