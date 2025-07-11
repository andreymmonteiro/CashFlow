using TransactionService.Domain.Events;

namespace TransactionService.Application.Commands
{
    public record CreateTransactionCommand(Guid TransactionId, Guid AccountId, decimal Amount)
    {
        public static explicit operator TransactionCreatedEvent(CreateTransactionCommand command)
            => new(command.TransactionId, command.AccountId, command.Amount, DateTime.UtcNow);
    }
}
