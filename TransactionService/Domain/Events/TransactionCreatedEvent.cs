using System.Text.Json;
using Domain.Shared;

namespace TransactionService.Domain.Events;

public record TransactionCreatedEvent(Guid TransactionId, Guid AccountId, decimal Amount, DateTime CreatedAt) : IDomainEvent
{
    public DateTime OccurredOn => CreatedAt;
}