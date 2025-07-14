using BalanceService.Application.Commands;
using BalanceService.Domain.Events;
using BalanceService.Domain.ValueObjects;

namespace BalanceService.Domain.Aggregates
{
    public sealed class Balance
    {
        public Guid AccountId { get; }

        public decimal Amount { get; }

        private Balance(string accountId, decimal amount) 
        {
            if(!Guid.TryParse(accountId, out var accountIdConverted))
            {
                throw new InvalidOperationException("Account id is not valid Guid.");
            }

            AccountId = accountIdConverted;
            Amount = amount;
        }

        public static Balance Create(string accountId, decimal amount)
            => new (accountId, amount);

        public static CreateBalanceCommand CreateCommand(Guid accountId, decimal debit, decimal credit, DateTime date)
        {
            BalanceAmount balanceAmount = (debit, credit);

            return new(accountId.ToString(), balanceAmount, date);
        }

        public BalanceCreatedEvent ToCreatedEvent()
            => new(AccountId.ToString(), Amount);
    }
}
