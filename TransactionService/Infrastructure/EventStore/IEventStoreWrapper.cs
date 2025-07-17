using EventStore.Client;

namespace TransactionService.Infrastructure.EventStore;

public interface IEventStoreWrapper
{
    Task<IWriteResult> AppendToStreamAsync(string streamName, StreamState expectedState, IEnumerable<EventData> eventData, Action<EventStoreClientOperationOptions>? configureOperationOptions = null, TimeSpan? deadline = null, UserCredentials? userCredentials = null, CancellationToken cancellationToken = default(CancellationToken));
}
