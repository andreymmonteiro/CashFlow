using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using EventStore.Client;

namespace Transaction.Tests.Integration
{
    public class EventStoreFixture : IAsyncLifetime
    {
        public EventStoreClient Client { get; private set; }

        private readonly TestcontainersContainer _container;

        public EventStoreFixture()
        {
            _container = new TestcontainersBuilder<TestcontainersContainer>()
                .WithImage("eventstore/eventstore:latest")
                .WithName("test-eventstore")
                .WithPortBinding(2113, true)
                .WithEnvironment("EVENTSTORE_INSECURE", "true")
                .WithEnvironment("EVENTSTORE_ENABLE_ATOM_PUB_OVER_HTTP", "true")
                .Build();
        }

        public async Task InitializeAsync()
        {
            await _container.StartAsync();

            var settings = EventStoreClientSettings.Create($"esdb://localhost:{_container.GetMappedPublicPort(2113)}?tls=false");
            Client = new EventStoreClient(settings);
        }

        public async Task DisposeAsync()
        {
            await _container.StopAsync();
        }
    }

}
