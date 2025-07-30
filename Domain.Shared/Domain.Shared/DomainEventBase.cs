namespace Domain.Shared;

public abstract record DomainEventBase : IDomainEvent
{
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
}
