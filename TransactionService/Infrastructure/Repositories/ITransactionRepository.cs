using MongoDB.Driver;
using TransactionService.Infrastructure.Projections;

namespace TransactionService.Infrastructure.Repositories
{
    public interface ITransactionRepository
    {
        Task<UpdateResult> SaveAsync(TransactionProjection transaction, string streamId, CancellationToken cancellationToken);
    }
}
