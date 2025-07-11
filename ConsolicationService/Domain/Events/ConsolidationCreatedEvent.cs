using ConsolicationService.Application.Commands;
using ConsolicationService.Domain.ValueObjects;

namespace ConsolicationService.Domain.Events
{
    public record ConsolidationCreatedEvent(Guid ConsolidationId, decimal Credit, decimal Debit, DateTime Date)
    {
        public static explicit operator ConsolidationCreatedEvent(CreateConsolidationCommand command)
        {
            ConsolidationAmount consolidationAmount = command.Amount;

            return new ConsolidationCreatedEvent(
                Guid.NewGuid(),
                consolidationAmount.Credit,
                consolidationAmount.Debit,
                command.CreatedAt);
        }
    }
}
