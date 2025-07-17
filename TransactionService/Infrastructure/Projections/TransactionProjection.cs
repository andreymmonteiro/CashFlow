using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TransactionService.Infrastructure.Projections;

public record TransactionProjection
{
    [BsonElement("_id")]
    public ObjectId Id { get; }

    public string TransactionId { get; set; }

    public string AccountId { get; set; }

    public decimal Amount { get; set; }

    public DateTime CreatedAt { get; set; }

    public string AppliedStreamId { get; set; }

    public TransactionProjection(string transactionId, string accountId, decimal amount, DateTime createdAt)
    {
        TransactionId = transactionId;
        AccountId = accountId;
        Amount = amount;
        CreatedAt = createdAt;
    }
}
