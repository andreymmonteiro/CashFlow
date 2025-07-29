using Domain.Shared;
using TransactionService.Domain.Events;

namespace TransactionService.Domain.Aggregates
{
    public sealed class Transaction : AggregateRoot
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

            var dataTimeNow = DateTime.UtcNow;

            var transactionId = Guid.NewGuid();

            var transactionCreatedEvent = new TransactionCreatedEvent(transactionId, accountId, amount, dataTimeNow);

            var transaction = new Transaction(transactionId, accountId, amount, dataTimeNow);

            transaction.AddDomainEvent(transactionCreatedEvent);

            return transaction;
        }
    }
}
