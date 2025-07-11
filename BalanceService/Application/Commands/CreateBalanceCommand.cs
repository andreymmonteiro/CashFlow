using BalanceService.Domain.Events;
using BalanceService.Domain.ValueObjects;

namespace BalanceService.Application.Commands
{
    public class CreateBalanceCommand
    {
        public string AccountId { get; }
        
        public decimal Amount { get; }
        
        public CreateBalanceCommand(string accountId, decimal amount)
        {
            AccountId = accountId;
            Amount = amount;
        }

        public static explicit operator CreateBalanceCommand(ConsolidationCreatedEvent @event)
        {
            BalanceAmount balanceAmount = (@event.Debit, @event.Credit);

            return new CreateBalanceCommand(
                @event.AccountId.ToString(),
                balanceAmount.Amount
            );
        }

        public static explicit operator BalanceCreatedEvent(CreateBalanceCommand command)
            => new(command.AccountId, command.Amount);
    }
}