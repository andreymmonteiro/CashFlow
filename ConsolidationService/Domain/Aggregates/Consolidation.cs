using ConsolidationService.Domain.Events;
using ConsolidationService.Domain.ValueObjects;

namespace ConsolidationService.Domain.Aggregates;

public sealed class Consolidation
{
    public Guid AccountId { get; }

    public ConsolidationAmount Amount { get; }

    public DateTime Date { get; }

    private Consolidation(Guid accountId, ConsolidationAmount amount, DateTime date)
    {
        AccountId = accountId;
        Date = date;
        Amount = amount;
    }

    public static Consolidation Create(Guid accountId, decimal amount, DateTime createdAt)
        => new(accountId, amount, createdAt);

    public ConsolidationCreatedEvent ToCreatedEvent()
        => new(AccountId, Amount.Credit, Amount.Debit, Date);

    //public static CreateConsolidationCommand CreateCommand(Guid accountId, decimal amount, DateTime date)
    //    => new(
    //        accountId.ToString(),
    //        amount,
    //        date);
}
