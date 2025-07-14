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
    }
}