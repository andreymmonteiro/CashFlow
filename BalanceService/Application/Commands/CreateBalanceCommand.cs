namespace BalanceService.Application.Commands;

public class CreateBalanceCommand
{
    public Guid AccountId { get; }

    public decimal Debit { get; }

    public decimal Credit { get; }

    public DateTime Date { get; }

    public CreateBalanceCommand(Guid accountId, decimal debit, decimal credit, DateTime date)
    {
        AccountId = accountId;
        Debit = debit;
        Credit = credit;
        Date = date;
    }
}