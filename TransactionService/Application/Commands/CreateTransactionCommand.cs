using TransactionService.Domain.Events;

namespace TransactionService.Application.Commands
{
    public record CreateTransactionCommand(Guid AccountId, decimal Amount)
    {
        public static explicit operator TransactionCreatedEvent(CreateTransactionCommand command)
            => new(Guid.NewGuid(), command.AccountId, command.Amount, DateTime.UtcNow);
    }
}
