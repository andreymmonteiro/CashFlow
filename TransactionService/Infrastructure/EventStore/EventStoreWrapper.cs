using EventStore.Client;
using Grpc.Core;
using Polly;

namespace TransactionService.Infrastructure.EventStore;

public class EventStoreWrapper : IEventStoreWrapper
{
    private readonly EventStoreClient _eventStoreClient;
    private readonly ILogger<EventStoreWrapper> _logger;

    public EventStoreWrapper(EventStoreClient eventStoreClient, ILogger<EventStoreWrapper> logger)
    {
        _eventStoreClient = eventStoreClient;
        _logger = logger;
    }

    public async Task<IWriteResult> AppendToStreamAsync(string streamName, StreamState expectedState, IEnumerable<EventData> eventData, Action<EventStoreClientOperationOptions>? configureOperationOptions = null, TimeSpan? deadline = null, UserCredentials? userCredentials = null, CancellationToken cancellationToken = default)
    {
        IWriteResult result = null;

        // Retry EventStore append on RpcException
        await Policy
            .Handle<RpcException>(ex =>
                ex.StatusCode == StatusCode.Unavailable
             || ex.StatusCode == StatusCode.ResourceExhausted
             || ex.StatusCode == StatusCode.DeadlineExceeded)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(100 * attempt),
                onRetry: (ex, ts, retryCount, _) =>
                    _logger.LogWarning(ex, "RpcException, retry {RetryCount}", retryCount)
            )
            .ExecuteAsync(async () =>
            {
                result = await _eventStoreClient.AppendToStreamAsync(
                     streamName,
                     expectedState,
                     eventData,
                     configureOperationOptions,
                     deadline,
                     userCredentials,
                     cancellationToken);
            });

        return result;
    }
}
