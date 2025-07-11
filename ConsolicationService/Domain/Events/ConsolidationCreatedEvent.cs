namespace ConsolicationService.Domain.Events
{
    public record ConsolidationCreatedEvent(Guid AccountId, decimal Credit, decimal Debit, DateTime Date);
}
