using System.Text.Json;
using BalanceService.Domain.Events;
using BalanceService.Infrastructure.EventStore;
using BalanceService.Infrastructure.Messaging.Channel;
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
        private readonly ICreatedBalancePublisherChannel _publisherChannel;
        private readonly IMongoCollection<BalanceProjection> _balances;
        private readonly ILogger<CreateBalanceCommandHandler> _logger;

        public CreateBalanceCommandHandler(IEventStoreWrapper eventStore, ICreatedBalancePublisherChannel publisherChannel, IMongoCollection<BalanceProjection> balances, ILogger<CreateBalanceCommandHandler> logger)
        {
            _publisherChannel = publisherChannel;
            _logger = logger;
            _balances = balances;
            _eventStore = eventStore;
        }

        public async Task<long> HandleAsync(CreateBalanceCommand command, CancellationToken cancellationToken)
        {
            var channel = await _publisherChannel.CreateChannelAsync();

            var properties = new BasicProperties
            {
                Persistent = true
            };

            var @event = (BalanceCreatedEvent)command;

            var date = command.Date;

            var id = DeterministicId.For(command.AccountId, date);

            var eventId = Uuid.FromGuid(id);

            var balanceId = id.ToString();

            try
            {
                _logger.LogInformation("Handling CreateBalanceCommand for AccountId: {AccountId}, Amount: {Amount}",
                                    command.AccountId, command.Amount);

                await InsertEventAsync(@event, balanceId, eventId, cancellationToken);

                var filter = Builders<BalanceProjection>.Filter.And(
                    Builders<BalanceProjection>.Filter.Eq(c => c.AccountId, command.AccountId),
                    Builders<BalanceProjection>.Filter.Not(
                        Builders<BalanceProjection>.Filter.AnyEq(c => c.AppliedTransactionIds, balanceId))
                );

                var update = Builders<BalanceProjection>.Update
                     .SetOnInsert(c => c.AccountId, command.AccountId)
                     .Inc(c => c.Amount, @event.Amount)
                     .Push(c => c.AppliedTransactionIds, balanceId);

                var options = new UpdateOptions { IsUpsert = true };
                var upsertResult = await _balances.UpdateOneAsync(filter, update, options, cancellationToken);

                var body = JsonSerializer.SerializeToUtf8Bytes(@event);

                await channel.BasicPublishAsync(
                    exchange: "",
                    routingKey: "balance.created",
                    mandatory: true,
                    basicProperties: properties,
                    body: body,
                    cancellationToken: cancellationToken);

                _logger.LogInformation("Published BalanceCreatedEvent to RabbitMQ");

                return upsertResult.ModifiedCount;
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
                    ConsolidationId = balanceId
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
