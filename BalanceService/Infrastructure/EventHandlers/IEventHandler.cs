using Domain.Shared;

namespace BalanceService.Infrastructure.EventHandlers
{
    public interface IEventHandler<IEvent> where IEvent : IDomainEvent
    {
        Task HandleAsync(IEvent @event, string streamId, CancellationToken cancellationToken);
    }
}
