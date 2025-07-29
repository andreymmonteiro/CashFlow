using Domain.Shared;

namespace TransactionService.Infrastructure.Messaging.Publishers;

public interface IPublisherHandler<IEvent> where IEvent : IDomainEvent
{
    Task HandleAsync(IEvent @event, CancellationToken cancellationToken);
}
