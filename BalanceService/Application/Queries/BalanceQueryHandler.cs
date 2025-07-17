using BalanceService.Application.Commands;
using BalanceService.Infrastructure.Projections;
using BalanceService.Presentation.Dtos.Request;
using BalanceService.Presentation.Dtos.Response;
using MongoDB.Driver;

namespace BalanceService.Application.Queries;

public class BalanceQueryHandler : IQueryHandler<BalanceRequest, BalanceResponse>
{
    private readonly IMongoCollection<BalanceProjection> _balances;
    private readonly ILogger<CreateBalanceCommandHandler> _logger;

    public BalanceQueryHandler(IMongoCollection<BalanceProjection> balances, ILogger<CreateBalanceCommandHandler> logger)
    {
        _balances = balances;
        _logger = logger;
    }

    public async Task<BalanceResponse> HandleAsync(BalanceRequest parameter, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _balances.Find(f => f.AccountId == parameter.AccountId).FirstOrDefaultAsync(cancellationToken);

            return (BalanceResponse)result;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error occurred while handling DailyConsolidationQuery for AccountId: {AccountId}",
                parameter.AccountId);
            throw;
        }
    }
}
