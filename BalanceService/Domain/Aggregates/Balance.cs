using BalanceService.Application.Commands;
using BalanceService.Domain.Events;
using BalanceService.Domain.ValueObjects;

namespace BalanceService.Domain.Aggregates;

public sealed class Balance
{
    public Guid AccountId { get; }

    public BalanceAmount Amount { get; }

    private Balance(Guid accountId, decimal debit, decimal credit)
    {
        AccountId = accountId;
        Amount = (debit, credit);
    }

    public static Balance Create(Guid accountId, decimal debit, decimal credit)
        => new(accountId, debit, credit);

    public BalanceCreatedEvent ToCreatedEvent()
        => new(AccountId.ToString(), Amount);
}