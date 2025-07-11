using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TransactionService.Infrastructure.Projections
{
    public record TransactionProjection
    {
        [BsonElement("_id")]
        public ObjectId Id { get; set; } = ObjectId.GenerateNewId();

        public string TransactionId { get; set; }
        
        public string AccountId { get; set; }

        public decimal Amount { get; set; }
        
        public DateTime CreatedAt { get; set; }

        public IList<string> AppliedTransactionIds { get; set; } = new List<string>();

        public TransactionProjection(string transactionId, string accountId, decimal amount, DateTime createdAt)
        {
            TransactionId = transactionId;
            AccountId = accountId;
            Amount = amount;
            CreatedAt = createdAt;
        }
    }
}
