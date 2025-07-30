using BalanceService.Domain.Events;
using BalanceService.Domain.ValueObjects;
using Domain.Shared;

namespace BalanceService.Domain.Aggregates;

public sealed class Balance : AggregateRoot
{
    public Guid AccountId { get; }

    public BalanceAmount Amount { get; }

    private Balance(Guid accountId, decimal debit, decimal credit)
    {
        AccountId = accountId;
        Amount = (debit, credit);        
    }

    public static Balance Create(Guid accountId, decimal debit, decimal credit)
    {
        var balance = new Balance(accountId, debit, credit);

        balance.AddDomainEvent(new BalanceCreatedEvent(balance.AccountId, balance.Amount));

        return balance;
    }
}