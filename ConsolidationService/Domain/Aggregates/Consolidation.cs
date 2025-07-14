using ConsolidationService.Application.Commands;
using ConsolidationService.Domain.Events;
using ConsolidationService.Domain.ValueObjects;

namespace ConsolidationService.Domain.Aggregates
{
    public sealed class Consolidation
    {
        public Guid AccountId { get; }

        public ConsolidationAmount Amount { get; }
        
        public DateTime Date { get; }

        private Consolidation(string accountId, ConsolidationAmount amount, DateTime date)
        {
            if (!Guid.TryParse(accountId, out var accountIdConverted))
            {
                throw new InvalidOperationException("Account id is not valid Guid.");
            }

            AccountId = accountIdConverted;
            Date = date;
            Amount = amount;
        }

        public static Consolidation Create(string accountId, decimal amount, DateTime createdAt)
            => new(accountId, amount, createdAt);

        public ConsolidationCreatedEvent ToCreatedEvent()
            => new (AccountId, Amount.Credit, Amount.Debit, Date);

        public static CreateConsolidationCommand CreateCommand(Guid accountId, decimal amount, DateTime date)
            => new(
                accountId.ToString(),
                amount,
                date);
    }
}
