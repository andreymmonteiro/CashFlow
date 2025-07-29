using TransactionService.Domain.Events;
using TransactionService.Infrastructure.Projections;
using TransactionService.Infrastructure.Repositories;

namespace TransactionService.Infrastructure.EventHandlers;

public sealed class TransactionCreatedProjectionHandler : IEventHandler<TransactionCreatedEvent>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly ILogger<TransactionCreatedProjectionHandler> _logger;

    public TransactionCreatedProjectionHandler(
        ITransactionRepository transactionRepository, 
        ILogger<TransactionCreatedProjectionHandler> logger)
    {
        _transactionRepository = transactionRepository;
        _logger = logger;
    }

    public async Task HandleAsync(TransactionCreatedEvent @event, string streamId, CancellationToken cancellationToken)
    {
        var projection = new TransactionProjection(@event.TransactionId.ToString(), @event.AccountId.ToString(), @event.Amount, @event.CreatedAt);

        var updateResult = await _transactionRepository.SaveAsync(projection, streamId, cancellationToken);

        _logger.LogInformation("MongoDB upsert result: {Result}", updateResult);
    }
}