using System.Text.Json;
using ConsolidationService.Domain.Aggregates;
using ConsolidationService.Domain.Events;
using ConsolidationService.Infrastructure.EventStore;
using ConsolidationService.Infrastructure.Projections;
using ConsolidationService.Infrastructure.Utilities;
using EventStore.Client;
using Grpc.Core;
using MongoDB.Driver;
using Polly;
using RabbitMQ.Client;
using StreamTail.Channels;

namespace ConsolidationService.Application.Commands;

public class CreateConsolidationCommandHandler : ICommandHandler<CreateConsolidationCommand, long>
{
    private readonly IEventStoreWrapper _eventStore;
    private readonly IChannelPool _channelPool;
    private readonly IMongoCollection<ConsolidationProjection> _consolidations;
    private readonly ILogger<CreateConsolidationCommandHandler> _logger;

    public CreateConsolidationCommandHandler(IEventStoreWrapper eventStore, IChannelPool channelPool, IMongoCollection<ConsolidationProjection> consolidations, ILogger<CreateConsolidationCommandHandler> logger)
    {
        _eventStore = eventStore;
        _channelPool = channelPool;
        _consolidations = consolidations;
        _logger = logger;
    }

    public async Task<long> HandleAsync(CreateConsolidationCommand command, CancellationToken cancellationToken)
    {
        var properties = new BasicProperties
        {
            Persistent = true
        };

        var consolidation = Consolidation.Create(command.AccountId, command.Amount, command.CreatedAt);

        var @event = consolidation.ToCreatedEvent();

        var accountId = command.AccountId.ToString();

        var id = DeterministicId.For(accountId, @event.Date);

        var eventId = Uuid.FromGuid(id);

        await using var lease = await _channelPool.RentAsync(cancellationToken);

        var channel = lease.Channel;

        await channel.QueueDeclareAsync("consolidation-created", durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);

        var streamId = id.ToString();

        try
        {
            _logger.LogInformation("Handling CreateConsolidationCommand for AccountId: {AccountId}, Amount: {Amount}, CreatedAt: {CreatedAt}",
                command.AccountId, command.Amount, command.CreatedAt);

            await InsertEventAsync(@event, streamId, eventId, cancellationToken);

            var modifiedCount = await UpsertProjectionAsync(accountId, consolidation, streamId, cancellationToken);

            var body = JsonSerializer.SerializeToUtf8Bytes(@event);

            await channel.BasicPublishAsync(
                exchange: "",
                routingKey: "consolidation-created",
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Published ConsolidationCreatedEvent to RabbitMQ");

            return modifiedCount;
        }
        catch (Exception e)
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
                routingKey: "consolidation-dlq",
                mandatory: true,
                basicProperties: properties,
                body: dlqBody,
                cancellationToken: cancellationToken);

            return 0;
        }
        finally
        {
            if (channel is { IsOpen: true })
            {
                await channel.CloseAsync();
            }
        }
    }

    private async Task<long> UpsertProjectionAsync(string accountId, Consolidation consolidation, string streamId, CancellationToken cancellationToken)
    {
        var filter = Builders<ConsolidationProjection>.Filter.And(
            Builders<ConsolidationProjection>.Filter.Eq(c => c.AccountId, accountId),
            Builders<ConsolidationProjection>.Filter.Eq(c => c.Date, consolidation.Date.Date),
            Builders<ConsolidationProjection>.Filter.Not(
                Builders<ConsolidationProjection>.Filter.AnyEq(c => c.AppliedStreamIds, streamId))
        );

        var update = Builders<ConsolidationProjection>.Update
            .SetOnInsert(c => c.AccountId, accountId)
            .SetOnInsert(c => c.Date, consolidation.Date.Date)
            .SetOnInsert(c => c.TotalAmount, consolidation.Amount.TotalAmount)
            .Inc(c => c.TotalDebits, consolidation.Amount.Debit)
            .Inc(c => c.TotalCredits, consolidation.Amount.Credit)
            .AddToSet(c => c.AppliedStreamIds, streamId);


        var options = new UpdateOptions { IsUpsert = false };
        var upsertResult = await _consolidations.UpdateOneAsync(filter, update, options, cancellationToken);

        if (upsertResult.MatchedCount == 0)
        {

            var documentFilter =
                Builders<ConsolidationProjection>.Filter.And(
                    Builders<ConsolidationProjection>.Filter.Eq(c => c.AccountId, accountId),
                    Builders<ConsolidationProjection>.Filter.Eq(c => c.Date, consolidation.Date.Date));

            var count = await _consolidations.CountDocumentsAsync(documentFilter, cancellationToken: cancellationToken);

            if (count > 0)
            {
                return 0; // Already exists, no need to insert
            }

            var projection = new ConsolidationProjection()
            {
                AccountId = accountId,
                Date = consolidation.Date.Date,
                TotalAmount = consolidation.Amount.TotalAmount,
                TotalDebits = consolidation.Amount.Debit,
                TotalCredits = consolidation.Amount.Credit,
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
