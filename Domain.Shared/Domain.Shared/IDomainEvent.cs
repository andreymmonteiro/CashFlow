namespace Domain.Shared;

public interface IDomainEvent
{
    DateTime CreatedAt { get; }
}
