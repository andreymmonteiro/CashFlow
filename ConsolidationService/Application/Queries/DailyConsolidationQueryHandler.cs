using ConsolidationService.Infrastructure.Projections;
using ConsolidationService.Presentation.Dtos.Request;
using ConsolidationService.Presentation.Dtos.Response;
using MongoDB.Driver;

namespace ConsolidationService.Application.Queries;

public class DailyConsolidationQueryHandler : IQueryHandler<DailyConsolidationRequest, DailyConsolidationResponse>
{
    private readonly IMongoCollection<ConsolidationProjection> _consolidations;
    private readonly ILogger<DailyConsolidationQueryHandler> _logger;

    public DailyConsolidationQueryHandler(IMongoCollection<ConsolidationProjection> consolidations, ILogger<DailyConsolidationQueryHandler> logger)
    {
        _consolidations = consolidations;
        _logger = logger;
    }

    public async Task<DailyConsolidationResponse> HandleAsync(DailyConsolidationRequest parameter, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Handling DailyConsolidationQuery for AccountId: {AccountId}, StartDate: {StartDate}, EndDate: {EndDate}",
                parameter.AccountId, parameter.StartDate, parameter.EndDate);

            var accountId = parameter.AccountId.ToString();
            var endDate = parameter.EndDate.Date.AddDays(1);

            var consolidationsResult = await _consolidations
                .Find(c => c.AccountId == accountId && c.Date >= parameter.StartDate.Date && c.Date < endDate.Date)
                .ToListAsync(cancellationToken);

            var items = consolidationsResult
                .Select(s => (DailyConsolidationItemResponse)s)
                .ToList();

            return new DailyConsolidationResponse(parameter.StartDate, parameter.EndDate, items);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error occurred while handling DailyConsolidationQuery for AccountId: {AccountId}, StartDate: {StartDate}, EndDate: {EndDate}",
                parameter.AccountId, parameter.StartDate, parameter.EndDate);
            throw;
        }
    }
}
