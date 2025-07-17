namespace BalanceService.Domain.Events;

public record BalanceCreatedEvent(string AccountId, decimal Amount);
