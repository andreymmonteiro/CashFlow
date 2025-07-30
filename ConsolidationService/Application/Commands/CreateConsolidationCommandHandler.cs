using System.Text.Json;
using ConsolidationService.Domain.Aggregates;
using ConsolidationService.Domain.Events;
using ConsolidationService.Infrastructure.EventStore;
using ConsolidationService.Infrastructure.Logging;
using ConsolidationService.Infrastructure.Utilities;
using EventStore.Client;
using Grpc.Core;
using Polly;
using RabbitMQ.Client;

namespace ConsolidationService.Application.Commands;

public class CreateConsolidationCommandHandler : ICommandHandler<CreateConsolidationCommand>
{
    private readonly IEventStoreWrapper _eventStore;
    private readonly IExceptionNotifier _exceptionNotifier;
    private readonly ILogger<CreateConsolidationCommandHandler> _logger;

    public CreateConsolidationCommandHandler(IEventStoreWrapper eventStore, IExceptionNotifier exceptionNotifier, ILogger<CreateConsolidationCommandHandler> logger)
    {
        _eventStore = eventStore;
        _exceptionNotifier = exceptionNotifier;
        _logger = logger;
    }

    public async Task HandleAsync(CreateConsolidationCommand command, CancellationToken cancellationToken)
    {
        var properties = new BasicProperties
        {
            Persistent = true
        };

        var consolidation = Consolidation.Create(command.AccountId, command.Amount, command.CreatedAt);

        var accountId = command.AccountId.ToString();

        var id = DeterministicId.For(accountId, consolidation.Date);

        var eventId = Uuid.FromGuid(id);

        var streamId = id.ToString();

        try
        {
            _logger.LogInformation("Handling CreateConsolidationCommand for AccountId: {AccountId}, Amount: {Amount}, CreatedAt: {CreatedAt}",
                command.AccountId, command.Amount, command.CreatedAt);

            await InsertEventAsync(consolidation, streamId, eventId, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error handling CreateConsolidationCommand for AccountId: {AccountId}", command.AccountId);

            var json = JsonSerializer.Serialize(command);

            await _exceptionNotifier.Notify(e, "consolidation-dlq", json, cancellationToken);
        }
    }

    private async Task InsertEventAsync(Consolidation consolidation, string consolidationId, Uuid eventId, CancellationToken cancellationToken)
    {
        var streamName = $"consolidation-{consolidationId}";
        var expectedVersion = StreamState.Any;

        var eventDataBatch = consolidation.DomainEvents.Select(s =>
            new EventData(
                eventId,
                nameof(ConsolidationCreatedEvent),
                JsonSerializer.SerializeToUtf8Bytes(s, s.GetType())));

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
                    eventDataBatch,
                    cancellationToken: cancellationToken);
            });
    }
}
