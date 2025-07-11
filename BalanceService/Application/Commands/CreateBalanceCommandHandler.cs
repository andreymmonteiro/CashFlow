using System.Text.Json;
using BalanceService.Domain.Events;
using BalanceService.Infrastructure.Messaging.Channel;
using BalanceService.Infrastructure.Utilities;
using EventStore.Client;
using Grpc.Core;
using Polly;
using RabbitMQ.Client;
using static EventStore.Client.StreamMessage;

namespace BalanceService.Application.Commands
{
    public class CreateBalanceCommandHandler : ICommandHandler<CreateBalanceCommand, long>
    {
        private readonly EventStoreClient _eventStore;
        private readonly ICreatedBalancePublisherChannel _publisherChannel;
        private readonly ILogger<CreateBalanceCommandHandler> _logger;

        public CreateBalanceCommandHandler(EventStoreClient eventStore, ICreatedBalancePublisherChannel publisherChannel, ILogger<CreateBalanceCommandHandler> logger)
        {
            _publisherChannel = publisherChannel;
            _logger = logger;
            _eventStore = eventStore;
        }

        public async Task<long> HandleAsync(CreateBalanceCommand command, CancellationToken cancellationToken)
        {
            var properties = new BasicProperties
            {
                Persistent = true
            };

            var id = DeterministicId.For(command.AccountId, command.CreatedAt);

            var eventId = Uuid.FromGuid(id);

            var channel = _publisherChannel.Channel;

            var consolidationId = id.ToString();

            try
            {
                var @event = (BalanceCreatedEvent)command;

                await InsertEventAsync(@event, consolidationId, eventId, cancellationToken);
            }
            catch(Exception e)
            {

            }
        }

        private async Task InsertEventAsync(BalanceCreatedEvent @event, string consolidationId, Uuid eventId, CancellationToken cancellationToken)
        {
            var streamName = $"balance-{consolidationId}";
            var expectedVersion = StreamState.Any;

            var eventData = new EventData(
                eventId,
                nameof(BalanceCreatedEvent),
                JsonSerializer.SerializeToUtf8Bytes(@event));

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
                    await _eventStore.AppendToStreamAsync(
                        streamName,
                        expectedVersion,
                        new[] { eventData },
                        cancellationToken: cancellationToken);
                });
        }
    }
}
