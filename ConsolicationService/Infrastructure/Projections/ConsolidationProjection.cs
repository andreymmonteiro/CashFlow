using MongoDB.Bson.Serialization.Attributes;

namespace ConsolicationService.Infrastructure.Projections
{
    public sealed class ConsolidationProjection
    {
        [BsonId]
        public DateTime Date { get; set; }

        public string AccountId { get; set; }

        public decimal TotalDebits { get; set; }

        public decimal TotalCredits { get; set; }

        public decimal TotalAmount { get; set; }

        public IList<string> AppliedTransactionIds { get; set; } = new List<string>();
    }
}
