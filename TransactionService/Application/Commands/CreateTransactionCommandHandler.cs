using System.Text.Json;
using EventStore.Client;
using TransactionService.Domain.Aggregates;
using TransactionService.Infrastructure.EventStore;
using TransactionService.Infrastructure.Logging;
using TransactionService.Infrastructure.Utilities;

namespace TransactionService.Application.Commands;

public sealed class CreateTransactionCommandHandler : ICommandHandler<CreateTransactionCommand, Guid>
{
    private readonly IEventStoreWrapper _eventStore;
    private readonly IExceptionNotifier _exceptionNotifier;
    private readonly ILogger<CreateTransactionCommandHandler> _logger;

    public CreateTransactionCommandHandler(
        IEventStoreWrapper eventStore,
        IExceptionNotifier exceptionNotifier,
        ILogger<CreateTransactionCommandHandler> logger)
    {
        _eventStore = eventStore;
        _exceptionNotifier = exceptionNotifier;
        _logger = logger;
    }

    public async Task<Guid> HandleAsync(CreateTransactionCommand command, CancellationToken cancellationToken)
    {
        var transaction = Transaction.Create(
            command.AccountId,
            command.Amount);

        var accountId = transaction.AccountId.ToString();

        var id = DeterministicId.For(accountId, transaction.CreatedAt);

        var streamId = id.ToString();

        var eventId = Uuid.FromGuid(id);

        var streamName = $"transaction-{streamId}";
        var expectedVersion = StreamState.Any;

        try
        {
            _logger.LogInformation("Handling CreateTransactionCommand for AccountId: {AccountId}, Amount: {Amount}, TransactionId: {TransactionId}",
                                command.AccountId, command.Amount, transaction.TransactionId);

            var eventDataBatch = transaction.DomainEvents.Select(s =>
                new EventData(
                    eventId,
                    s.GetType().Name,
                    JsonSerializer.SerializeToUtf8Bytes(s, s.GetType())));

            await _eventStore.AppendToStreamAsync(
                streamName,
                expectedVersion,
                eventDataBatch,
                cancellationToken: cancellationToken);

            return id;
        }
        catch (Exception ex)
        {
            await _exceptionNotifier.Notify(ex, "transaction-dlq", JsonSerializer.Serialize(command), cancellationToken);

            throw;
        }
    }
}


