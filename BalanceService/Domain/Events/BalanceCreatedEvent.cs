using Domain.Shared;

namespace BalanceService.Domain.Events;

public record BalanceCreatedEvent(Guid AccountId, decimal Amount) : IDomainEvent
{
    public DateTime OccurredOn => DateTime.UtcNow;
}
