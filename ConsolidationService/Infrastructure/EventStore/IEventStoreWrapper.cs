using EventStore.Client;

namespace ConsolidationService.Infrastructure.EventStore
{
    public interface IEventStoreWrapper
    {
        Task<IWriteResult> AppendToStreamAsync(string streamName, StreamState expectedState, IEnumerable<EventData> eventData, Action<EventStoreClientOperationOptions>? configureOperationOptions = null, TimeSpan? deadline = null, UserCredentials? userCredentials = null, CancellationToken cancellationToken = default(CancellationToken));
    }
}
