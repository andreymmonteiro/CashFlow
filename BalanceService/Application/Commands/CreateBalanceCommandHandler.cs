using System.Text.Json;
using BalanceService.Domain.Aggregates;
using BalanceService.Domain.Events;
using BalanceService.Infrastructure.EventStore;
using BalanceService.Infrastructure.Utilities;
using EventStore.Client;
using Grpc.Core;
using Polly;
using StreamTail.Logging;

namespace BalanceService.Application.Commands;

public class CreateBalanceCommandHandler : ICommandHandler<CreateBalanceCommand>
{
    private readonly IEventStoreWrapper _eventStore;
    private readonly IExceptionNotifier _exceptionNotifier;
    private readonly ILogger<CreateBalanceCommandHandler> _logger;

    public CreateBalanceCommandHandler(IEventStoreWrapper eventStore, IExceptionNotifier exceptionNotifier, ILogger<CreateBalanceCommandHandler> logger)
    {
        _eventStore = eventStore;
        _exceptionNotifier = exceptionNotifier;
        _logger = logger;
    }

    public async Task HandleAsync(CreateBalanceCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling CreateBalanceCommand for AccountId: {AccountId}, Debit: {Amount}, Credit: {Credit}",
                            command.AccountId, command.Debit, command.Credit);

        try
        {
            var accountId = command.AccountId.ToString();

            var balance = Balance.Create(command.AccountId, command.Debit, command.Credit);

            var date = command.Date;

            var id = DeterministicId.For(command.AccountId.ToString(), date);

            var eventId = Uuid.FromGuid(id);

            var streamId = id.ToString();

            await InsertEventAsync(balance, streamId, eventId, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error handling CreateBalanceCommand for AccountId: {AccountId}", command.AccountId);

            await _exceptionNotifier.Notify(e, "balance-dlq", JsonSerializer.Serialize(command), cancellationToken);
        }
    }

    private async Task InsertEventAsync(Balance balance, string balanceId, Uuid eventId, CancellationToken cancellationToken)
    {
        var streamName = $"balance-{balanceId}";
        var expectedVersion = StreamState.Any;

        var eventDataBatch = balance.DomainEvents.Select(s => new EventData(
            eventId,
            nameof(BalanceCreatedEvent),
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
