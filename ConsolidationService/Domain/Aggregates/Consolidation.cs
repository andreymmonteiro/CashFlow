using ConsolidationService.Domain.Events;
using ConsolidationService.Domain.ValueObjects;
using Domain.Shared;

namespace ConsolidationService.Domain.Aggregates;

public sealed class Consolidation : AggregateRoot
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
    {
        var consolidation = new Consolidation(accountId, amount, createdAt);

        consolidation.AddDomainEvent(
            new ConsolidationCreatedEvent(
                consolidation.AccountId, 
                consolidation.Amount.Credit, 
                consolidation.Amount.Debit, 
                consolidation.Amount.TotalAmount, 
                consolidation.Date));

        return consolidation;
    }
}
