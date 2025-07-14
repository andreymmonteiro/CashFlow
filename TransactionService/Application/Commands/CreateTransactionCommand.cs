namespace TransactionService.Application.Commands
{
    public record CreateTransactionCommand(Guid AccountId, decimal Amount);
}
