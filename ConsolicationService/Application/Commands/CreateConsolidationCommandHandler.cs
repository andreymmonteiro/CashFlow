using System.Text.Json;
using ConsolicationService.Domain.Events;
using ConsolicationService.Domain.ValueObjects;
using ConsolicationService.Infrastructure.EventStore;
using ConsolicationService.Infrastructure.Messaging.Channels;
using ConsolicationService.Infrastructure.Projections;
using ConsolicationService.Infrastructure.Utilities;
using EventStore.Client;
using Grpc.Core;
using MongoDB.Driver;
using Polly;
using RabbitMQ.Client;

namespace ConsolicationService.Application.Commands
{
    public class CreateConsolidationCommandHandler : ICommandHandler<CreateConsolidationCommand, long>
    {
        private readonly IEventStoreWrapper _eventStore;
        private readonly ICreatedConsolidationPublisherChannel _createdConsolidationPublisherChannel;
        private readonly IMongoCollection<ConsolidationProjection> _consolidations;
        private readonly ILogger<CreateConsolidationCommandHandler> _logger;

        public CreateConsolidationCommandHandler(IEventStoreWrapper eventStore, ICreatedConsolidationPublisherChannel createdConsolidationPublisherChannel, IMongoCollection<ConsolidationProjection> consolidations, ILogger<CreateConsolidationCommandHandler> logger)
        {
            _eventStore = eventStore;
            _createdConsolidationPublisherChannel = createdConsolidationPublisherChannel;
            _consolidations = consolidations;
            _logger = logger;
        }

        public async Task<long> HandleAsync(CreateConsolidationCommand command, CancellationToken cancellationToken)
        {
            var properties = new BasicProperties
            {
                Persistent = true
            };

            var @event = (ConsolidationCreatedEvent)command;

            var accountId = command.AccountId;

            var id = DeterministicId.For(accountId, @event.Date);

            var eventId = Uuid.FromGuid(id);

            var channel = await _createdConsolidationPublisherChannel.CreateChannelAsync();

            var consolidationId = id.ToString();

            try
            {
                _logger.LogInformation("Handling CreateConsolidationCommand for AccountId: {AccountId}, Amount: {Amount}, CreatedAt: {CreatedAt}",
                    command.AccountId, command.Amount, command.CreatedAt);                

                await InsertEventAsync(@event, consolidationId, eventId, cancellationToken);

                var dateOnly = command.CreatedAt.Date;

                var filter = Builders<ConsolidationProjection>.Filter.And(
                    Builders<ConsolidationProjection>.Filter.Eq(c => c.AccountId, command.AccountId),
                    Builders<ConsolidationProjection>.Filter.Eq(c => c.Date, command.CreatedAt.Date),
                    Builders<ConsolidationProjection>.Filter.Not(
                        Builders<ConsolidationProjection>.Filter.AnyEq(c => c.AppliedTransactionIds, consolidationId))
                );

                ConsolidationAmount consolidationAmount = command.Amount;

                var update = Builders<ConsolidationProjection>.Update
                    .SetOnInsert(c => c.AccountId, command.AccountId)
                    .SetOnInsert(c => c.Date, command.CreatedAt.Date)
                    .SetOnInsert(c => c.TotalAmount, consolidationAmount.TotalAmount)
                    .Inc(c => c.TotalDebits, consolidationAmount.Debit)
                    .Inc(c => c.TotalCredits, consolidationAmount.Credit)
                    .Push(c => c.AppliedTransactionIds, consolidationId);

                var options = new UpdateOptions { IsUpsert = true };
                var upsertResult = await _consolidations.UpdateOneAsync(filter, update, options, cancellationToken);

                var body = JsonSerializer.SerializeToUtf8Bytes(@event);

                await channel.BasicPublishAsync(
                    exchange: "",
                    routingKey: "consolidation.created",
                    mandatory: true,
                    basicProperties: properties,
                    body: body,
                    cancellationToken: cancellationToken);

                _logger.LogInformation("Published ConsolidationCreatedEvent to RabbitMQ");

                return upsertResult.ModifiedCount;
            }
            catch(Exception e)
            {
                _logger.LogError(e, "Error handling CreateConsolidationCommand for AccountId: {AccountId}", command.AccountId);

                // Send failed command to DLQ
                var dlqBody = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    FailedAt = DateTime.UtcNow,
                    Command = command,
                    Reason = e.Message,
                    ConsolidationId = consolidationId
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

        private async Task InsertEventAsync(ConsolidationCreatedEvent @event, string consolidationId, Uuid eventId, CancellationToken cancellationToken)
        {
            var streamName = $"consolidation-{consolidationId}";
            var expectedVersion = StreamState.Any;

            var eventData = new EventData(
                eventId,
                nameof(ConsolidationCreatedEvent),
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
