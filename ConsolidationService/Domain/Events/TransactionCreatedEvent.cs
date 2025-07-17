namespace ConsolidationService.Domain.Events;

public record TransactionCreatedEvent(Guid TransactionId, Guid AccountId, decimal Amount, DateTime CreatedAt);
