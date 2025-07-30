using MongoDB.Driver;
using TransactionService.Infrastructure.Projections;

namespace TransactionService.Infrastructure.Repositories
{
    public sealed class TransactionRepository : ITransactionRepository
    {
        private readonly IMongoCollection<TransactionProjection> _transaction;

        public TransactionRepository(IMongoCollection<TransactionProjection> transaction)
        {
            _transaction = transaction;
        }

        public async Task<UpdateResult> SaveAsync(TransactionProjection transaction, string streamId, CancellationToken cancellationToken)
        {
            var accountId = transaction.AccountId;
            var transactionId = transaction.TransactionId;
            var amount = transaction.Amount;
            var createdAt = transaction.CreatedAt;

            var filter = Builders<TransactionProjection>.Filter.And(
                Builders<TransactionProjection>.Filter.Eq(c => c.AccountId, accountId),
                Builders<TransactionProjection>.Filter.Eq(c => c.TransactionId, transactionId),
                Builders<TransactionProjection>.Filter.Eq(c => c.CreatedAt, createdAt),
                Builders<TransactionProjection>.Filter.Eq(c => c.AppliedStreamId, streamId)
                );

            var update = Builders<TransactionProjection>.Update
                 .SetOnInsert(c => c.AccountId, accountId)
                 .SetOnInsert(c => c.TransactionId, transactionId)
                 .SetOnInsert(c => c.CreatedAt, createdAt)
                 .SetOnInsert(c => c.Amount, amount)
                 .SetOnInsert(c => c.AppliedStreamId, streamId);

            var options = new UpdateOptions { IsUpsert = true };

            var updateResult = await _transaction.UpdateOneAsync(
                    filter: filter,
                    update: update,
                    options: options,
                    cancellationToken);

            return updateResult;
        }
    }
}
