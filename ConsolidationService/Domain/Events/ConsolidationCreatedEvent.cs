using Domain.Shared;

namespace ConsolidationService.Domain.Events;

public record ConsolidationCreatedEvent(Guid AccountId, decimal Credit, decimal Debit, decimal TotalAmount, DateTime Date) : IDomainEvent
{
    public DateTime OccurredOn => Date;
}
