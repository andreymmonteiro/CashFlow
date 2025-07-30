using ConsolidationService.Infrastructure.Projections;
using MongoDB.Driver;

namespace ConsolidationService.Infrastructure.Repositories;

public class ConsolidationRepository : IConsolidationRepository
{
    private readonly IMongoCollection<ConsolidationProjection> _consolidations;

    public ConsolidationRepository(IMongoCollection<ConsolidationProjection> consolidations)
    {
        _consolidations = consolidations;
    }

    public async Task SaveAsync(ConsolidationProjection projection, string streamId, CancellationToken cancellationToken)
    {
        var accountId = projection.AccountId.ToString();
        var date = projection.Date.Date;
        var debit = projection.TotalDebits;
        var credit = projection.TotalCredits;
        var totalAmount = projection.TotalAmount;

        var filter = Builders<ConsolidationProjection>.Filter.And(
            Builders<ConsolidationProjection>.Filter.Eq(c => c.AccountId, accountId),
            Builders<ConsolidationProjection>.Filter.Eq(c => c.Date, date),
            Builders<ConsolidationProjection>.Filter.Not(
                Builders<ConsolidationProjection>.Filter.AnyEq(c => c.AppliedStreamIds, streamId))
        );

        var update = Builders<ConsolidationProjection>.Update
            .SetOnInsert(c => c.AccountId, accountId)
            .SetOnInsert(c => c.Date, date)
            .SetOnInsert(c => c.TotalAmount, totalAmount)
            .Inc(c => c.TotalDebits, debit)
            .Inc(c => c.TotalCredits, credit)
            .AddToSet(c => c.AppliedStreamIds, streamId);


        var options = new UpdateOptions { IsUpsert = false };
        var upsertResult = await _consolidations.UpdateOneAsync(filter, update, options, cancellationToken);

        if (upsertResult.MatchedCount == 0)
        {

            var documentFilter =
                Builders<ConsolidationProjection>.Filter.And(
                    Builders<ConsolidationProjection>.Filter.Eq(c => c.AccountId, accountId),
                    Builders<ConsolidationProjection>.Filter.Eq(c => c.Date, date));

            var projectionResult = await _consolidations.Find(documentFilter).FirstOrDefaultAsync(cancellationToken: cancellationToken);

            if (projectionResult is not null)
            {
                return;
            }

            var insertOptions = new InsertOneOptions()
            {
                BypassDocumentValidation = true
            };

            await _consolidations.InsertOneAsync(projection, insertOptions, cancellationToken);

            return;
        }
    }
}