namespace Domain.Shared;

public abstract record DomainEventBase : IDomainEvent
{
    public DateTime OccurredOn { get; private set; } = DateTime.UtcNow;
}
