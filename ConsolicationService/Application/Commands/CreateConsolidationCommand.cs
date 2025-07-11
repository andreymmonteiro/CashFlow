using ConsolicationService.Domain.Events;
using ConsolicationService.Domain.ValueObjects;

namespace ConsolicationService.Application.Commands;

public record CreateConsolidationCommand(string AccountId, decimal Amount, DateTime CreatedAt) : Command
{
    public static explicit operator CreateConsolidationCommand(TransactionCreatedEvent @event)
        => new(
            @event.AccountId.ToString(),
            @event.Amount,
            @event.CreatedAt);

    public static explicit operator ConsolidationCreatedEvent(CreateConsolidationCommand command)
    {
        ConsolidationAmount consolidationAmount = command.Amount;

        return new ConsolidationCreatedEvent(
            Guid.Parse(command.AccountId),
            consolidationAmount.Credit,
            consolidationAmount.Debit,
            command.CreatedAt);
    }
}

