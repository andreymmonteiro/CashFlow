using ConsolidationService.Domain.Events;
using ConsolidationService.Infrastructure.Projections;
using ConsolidationService.Infrastructure.Repositories;

namespace ConsolidationService.Infrastructure.EventHandlers;

public sealed class ConsolidationCreatedProjectionHandler : IEventHandler<ConsolidationCreatedEvent>
{
    private readonly IConsolidationRepository _consolidationRepository;
    private readonly ILogger<ConsolidationCreatedProjectionHandler> _logger;

    public ConsolidationCreatedProjectionHandler(
        IConsolidationRepository consolidationRepository,
        ILogger<ConsolidationCreatedProjectionHandler> logger)
    {
        _consolidationRepository = consolidationRepository;
        _logger = logger;
    }

    public async Task HandleAsync(ConsolidationCreatedEvent @event, string streamId, CancellationToken cancellationToken)
    {
        var projection = new ConsolidationProjection()
        {
            AccountId = @event.AccountId.ToString(),
            Date = @event.Date.Date,
            TotalAmount = @event.TotalAmount,
            TotalDebits = @event.Debit,
            TotalCredits = @event.Credit,
            AppliedStreamIds = new List<string> { streamId }
        };

        await _consolidationRepository.SaveAsync(projection, streamId, cancellationToken);

        _logger.LogInformation("MongoDB upsert result");
    }
}