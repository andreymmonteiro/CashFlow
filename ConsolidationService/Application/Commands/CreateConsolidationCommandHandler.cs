using System.Text.Json;
using ConsolidationService.Domain.Events;
using ConsolidationService.Domain.ValueObjects;
using ConsolidationService.Infrastructure.EventStore;
using ConsolidationService.Infrastructure.Messaging.Channels;
using ConsolidationService.Infrastructure.Projections;
using ConsolidationService.Infrastructure.Utilities;
using EventStore.Client;
using Grpc.Core;
using MongoDB.Driver;
using Polly;
using RabbitMQ.Client;

namespace ConsolidationService.Application.Commands
{
    public class CreateConsolidationCommandHandler : ICommandHandler<CreateConsolidationCommand, long>
    {
        private readonly IEventStoreWrapper _eventStore;
        private readonly ICreatedConsolidationPublisherChannel _channel;
        private readonly IMongoCollection<ConsolidationProjection> _consolidations;
        private readonly ILogger<CreateConsolidationCommandHandler> _logger;

        public CreateConsolidationCommandHandler(IEventStoreWrapper eventStore, ICreatedConsolidationPublisherChannel channel, IMongoCollection<ConsolidationProjection> consolidations, ILogger<CreateConsolidationCommandHandler> logger)
        {
            _eventStore = eventStore;
            _channel = channel;
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

            var channel = await _channel.CreateChannelAsync();

            await channel.QueueDeclareAsync("consolidation.created", durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);

            var streamId = id.ToString();

            try
            {
                _logger.LogInformation("Handling CreateConsolidationCommand for AccountId: {AccountId}, Amount: {Amount}, CreatedAt: {CreatedAt}",
                    command.AccountId, command.Amount, command.CreatedAt);                

                await InsertEventAsync(@event, streamId, eventId, cancellationToken);

                var modifiedCount = await UpsertProjectionAsync(command, streamId, cancellationToken);

                var body = JsonSerializer.SerializeToUtf8Bytes(@event);

                await channel.BasicPublishAsync(
                    exchange: "",
                    routingKey: "consolidation.created",
                    mandatory: true,
                    basicProperties: properties,
                    body: body,
                    cancellationToken: cancellationToken);

                _logger.LogInformation("Published ConsolidationCreatedEvent to RabbitMQ");

                return modifiedCount;
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

        private async Task<long> UpsertProjectionAsync(CreateConsolidationCommand command, string streamId, CancellationToken cancellationToken)
        {
            var filter = Builders<ConsolidationProjection>.Filter.And(
                Builders<ConsolidationProjection>.Filter.Eq(c => c.AccountId, command.AccountId),
                Builders<ConsolidationProjection>.Filter.Eq(c => c.Date, command.CreatedAt.Date),
                Builders<ConsolidationProjection>.Filter.Not(
                    Builders<ConsolidationProjection>.Filter.AnyEq(c => c.AppliedStreamIds, streamId))
            );

            ConsolidationAmount consolidationAmount = command.Amount;

            var update = Builders<ConsolidationProjection>.Update
                .SetOnInsert(c => c.AccountId, command.AccountId)
                .SetOnInsert(c => c.Date, command.CreatedAt.Date)
                .SetOnInsert(c => c.TotalAmount, consolidationAmount.TotalAmount)
                .Inc(c => c.TotalDebits, consolidationAmount.Debit)
                .Inc(c => c.TotalCredits, consolidationAmount.Credit)
                .AddToSet(c => c.AppliedStreamIds, streamId);

            
            var options = new UpdateOptions { IsUpsert = false };
            var upsertResult = await _consolidations.UpdateOneAsync(filter, update, options, cancellationToken);

            if (upsertResult.MatchedCount == 0)
            {

                var documentFilter = 
                    Builders<ConsolidationProjection>.Filter.And(
                        Builders<ConsolidationProjection>.Filter.Eq(c => c.AccountId, command.AccountId),
                        Builders<ConsolidationProjection>.Filter.Eq(c => c.Date, command.CreatedAt.Date));

                var count = await _consolidations.CountDocumentsAsync(documentFilter, cancellationToken: cancellationToken);

                if(count > 0)
                {
                    return 0; // Already exists, no need to insert
                }

                var projection = new ConsolidationProjection()
                {
                    AccountId = command.AccountId,
                    Date = command.CreatedAt.Date,
                    TotalAmount = consolidationAmount.TotalAmount,
                    TotalDebits = consolidationAmount.Debit,
                    TotalCredits = consolidationAmount.Credit,
                    AppliedStreamIds = new List<string> { streamId }
                };

                var insertOptions = new InsertOneOptions()
                {
                    BypassDocumentValidation = true
                };

                await _consolidations.InsertOneAsync(projection, insertOptions, cancellationToken);

                return 1;
            }

            return upsertResult.ModifiedCount;
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
