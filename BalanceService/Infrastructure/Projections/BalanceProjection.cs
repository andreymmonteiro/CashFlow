using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BalanceService.Infrastructure.Projections
{
    public record BalanceProjection
    {
        [BsonElement("_id")]
        public ObjectId Id { get; set; }

        public string AccountId { get; set; }

        public decimal Amount { get; set; }

        public IList<string> AppliedTransactionIds { get; set; } = new List<string>();
    }
}
