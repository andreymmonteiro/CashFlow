using BalanceService.Infrastructure.Projections;
using MongoDB.Driver;

namespace BalanceService.Infrastructure.Repositories;

public class BalanceRepository : IBalanceRepository
{
    private readonly IMongoCollection<BalanceProjection> _balances;

    public BalanceRepository(IMongoCollection<BalanceProjection> balances)
    {
        _balances = balances;
    }

    public async Task SaveAsync(BalanceProjection projection, string streamId, CancellationToken cancellationToken)
    {
        var accountId = projection.AccountId;

        var filter = Builders<BalanceProjection>.Filter.And(
            Builders<BalanceProjection>.Filter.Eq(c => c.AccountId, accountId),
            Builders<BalanceProjection>.Filter.Not(
                Builders<BalanceProjection>.Filter.AnyEq(c => c.AppliedStreamIds, streamId))
        );

        var update = Builders<BalanceProjection>.Update
             .SetOnInsert(c => c.AccountId, accountId)
             .Inc(c => c.Amount, projection.Amount)
             .Push(c => c.AppliedStreamIds, streamId);

        var options = new UpdateOptions { IsUpsert = false };
        var upsertResult = await _balances.UpdateOneAsync(filter, update, options, cancellationToken);

        if (upsertResult.ModifiedCount == 0)
        {
            var documentFilter = Builders<BalanceProjection>.Filter.And(
                Builders<BalanceProjection>.Filter.Eq(c => c.AccountId, accountId));

            var projectionResult = await _balances.Find(documentFilter).FirstOrDefaultAsync(cancellationToken: cancellationToken);

            if (projectionResult is not null)
            {
                return;
            }

            var insertOptions = new InsertOneOptions()
            {
                BypassDocumentValidation = true
            };

            await _balances.InsertOneAsync(projection, insertOptions, cancellationToken);

            return;
        }
    }
}