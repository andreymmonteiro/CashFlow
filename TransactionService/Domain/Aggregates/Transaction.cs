using TransactionService.Domain.Events;

namespace TransactionService.Domain.Aggregates
{
    public sealed class Transaction
    {
        public Guid TransactionId { get; }

        public Guid AccountId { get; }

        public decimal Amount { get; }

        public DateTime CreatedAt { get; }

        private Transaction(Guid transactionId, Guid accountId, decimal amount, DateTime createdAt)
        {
            TransactionId = transactionId;
            AccountId = accountId;
            Amount = amount;
            CreatedAt = createdAt;
        }

        public static Transaction Create(Guid accountId, decimal amount)
        {
            if (amount == 0)
            {
                throw new InvalidOperationException("Amount cannot be zero.");
            }

            var transactionId = Guid.NewGuid();

            return new (transactionId, accountId, amount, DateTime.UtcNow);
        }

        public TransactionCreatedEvent ToCreatedEvent()
            => new(TransactionId, AccountId, Amount, CreatedAt);
    }
}
