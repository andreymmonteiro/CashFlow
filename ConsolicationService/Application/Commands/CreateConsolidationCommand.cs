using ConsolicationService.Domain.Events;

namespace ConsolicationService.Application.Commands;

public record CreateConsolidationCommand(string AccountId, decimal Amount, DateTime CreatedAt) : Command
{
    public static explicit operator CreateConsolidationCommand(TransactionCreatedEvent @event)
        => new(
            @event.AccountId.ToString(),
            @event.Amount,
            @event.CreatedAt);
}

