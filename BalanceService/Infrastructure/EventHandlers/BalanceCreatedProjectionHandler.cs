using BalanceService.Domain.Events;
using BalanceService.Infrastructure.Projections;
using BalanceService.Infrastructure.Repositories;

namespace BalanceService.Infrastructure.EventHandlers;

public sealed class BalanceCreatedProjectionHandler : IEventHandler<BalanceCreatedEvent>
{
    private readonly IBalanceRepository _balanceRepository;
    private readonly ILogger<BalanceCreatedProjectionHandler> _logger;

    public BalanceCreatedProjectionHandler(IBalanceRepository balanceRepository, ILogger<BalanceCreatedProjectionHandler> logger)
    {
        _balanceRepository = balanceRepository;
        _logger = logger;
    }

    public async Task HandleAsync(BalanceCreatedEvent @event, string streamId, CancellationToken cancellationToken)
    {
        var projection = new BalanceProjection
        {
            AccountId = @event.AccountId.ToString(),
            Amount = @event.Amount,
            AppliedStreamIds = new List<string> { streamId }
        };

        await _balanceRepository.SaveAsync(projection, streamId, cancellationToken);

        _logger.LogInformation("MongoDB upsert performed");
    }
}