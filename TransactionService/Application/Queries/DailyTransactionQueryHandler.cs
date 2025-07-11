using MongoDB.Driver;
using TransactionService.Infrastructure.Projections;
using TransactionService.Presentation.Dtos.Request;
using TransactionService.Presentation.Dtos.Response;

namespace TransactionService.Application.Queries
{
    public class DailyTransactionQueryHandler : IQueryHandler<DailyTransactionRequest, DailyTransactionResponse>
    {
        private readonly IMongoCollection<TransactionProjection> _transaction;
        private readonly ILogger<DailyTransactionQueryHandler> _logger;

        public DailyTransactionQueryHandler(IMongoCollection<TransactionProjection> transaction, ILogger<DailyTransactionQueryHandler> logger)
        {
            _transaction = transaction;
            _logger = logger;
        }

        public async Task<DailyTransactionResponse> HandleAsync(DailyTransactionRequest parameter, CancellationToken cancellationToken)
        {
            try
            {
                var endDate = parameter.EndDate.AddDays(1).Date;

                var result = await _transaction
                    .Find(x => x.AccountId == parameter.AccountId.ToString() && x.CreatedAt.Date >= parameter.StartDate.Date && x.CreatedAt < endDate)
                    .ToListAsync(cancellationToken);

                var items = result.Select(s => (DailyTransactionItemResponse)s);

                return new DailyTransactionResponse(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling daily transaction query");
                throw;
            }
        }
    }
}
