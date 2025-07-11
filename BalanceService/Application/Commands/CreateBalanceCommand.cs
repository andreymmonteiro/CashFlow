using BalanceService.Domain.Events;
using BalanceService.Domain.ValueObjects;

namespace BalanceService.Application.Commands
{
    public class CreateBalanceCommand
    {
        public string AccountId { get; }
        
        public decimal Amount { get; }
        
        public DateTime Date { get; }

        public CreateBalanceCommand(string accountId, decimal amount, DateTime date)
        {
            AccountId = accountId;
            Amount = amount;
            Date = date;
        }

        public static explicit operator CreateBalanceCommand(ConsolidationCreatedEvent @event)
        {
            BalanceAmount balanceAmount = (@event.Debit, @event.Credit);

            return new CreateBalanceCommand(
                @event.AccountId.ToString(),
                balanceAmount.Amount,
                @event.Date
            );
        }

        public static explicit operator BalanceCreatedEvent(CreateBalanceCommand command)
            => new(command.AccountId, command.Amount);
    }
}