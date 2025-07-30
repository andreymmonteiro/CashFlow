using Domain.Shared;

namespace BalanceService.Infrastructure.Messaging.Publishers;

public interface IPublisherHandler<IEvent> where IEvent : IDomainEvent
{
    Task HandleAsync(IEvent @event, CancellationToken cancellationToken);
}
