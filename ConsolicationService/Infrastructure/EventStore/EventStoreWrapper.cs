using EventStore.Client;

namespace ConsolicationService.Infrastructure.EventStore
{
    public class EventStoreWrapper : IEventStoreWrapper
    {
        private readonly EventStoreClient _eventStoreClient;

        public EventStoreWrapper(EventStoreClient eventStoreClient)
        {
            _eventStoreClient = eventStoreClient;
        }

        public async Task<IWriteResult> AppendToStreamAsync(string streamName, StreamState expectedState, IEnumerable<EventData> eventData, Action<EventStoreClientOperationOptions>? configureOperationOptions = null, TimeSpan? deadline = null, UserCredentials? userCredentials = null, CancellationToken cancellationToken = default)
        {
           return  await _eventStoreClient.AppendToStreamAsync(
                streamName,
                expectedState,
                eventData,
                configureOperationOptions,
                deadline,
                userCredentials,
                cancellationToken);
        }
    }
}
