namespace TransactionService.Infrastructure.Projections
{
    public record TransactionProjection
    {
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
