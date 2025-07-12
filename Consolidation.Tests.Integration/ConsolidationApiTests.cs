using System.Net;
using System.Net.Http.Json;
using ConsolicationService;
using ConsolicationService.Infrastructure.Messaging.Channels;
using ConsolicationService.Infrastructure.Projections;
using ConsolicationService.Presentation.Dtos.Request;
using ConsolicationService.Presentation.Dtos.Response;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using NSubstitute;
using RabbitMQ.Client;

namespace Consolidation.Tests.Integration
{
    public class ConsolidationApiTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public ConsolidationApiTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task GetConsolidations_ByAccountIdAndRangeDate_ReturnsConsolidations()
        {
            // Arrange
            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");

                builder.ConfigureServices(services => 
                {
                    var mongoClient = Substitute.For<IMongoClient>();

                    var database = Substitute.For<IMongoDatabase>();

                    var collection = Substitute.For<IMongoCollection<ConsolidationProjection>>();

                    services.AddSingleton(collection);

                    var createdTransactionConsumerChannel = Substitute.For<ICreatedTransactionConsumerChannel>();

                    createdTransactionConsumerChannel.CreateChannelAsync()
                        .Returns(Substitute.For<IChannel>());

                    var createdConsolidationPublisherChannel = Substitute.For<ICreatedConsolidationPublisherChannel>();

                    createdConsolidationPublisherChannel.CreateChannelAsync()
                        .Returns(Substitute.For<IChannel>());

                    var rabbitMqQueueInitializerChannel = Substitute.For<IRabbitMqQueueInitializerChannel>();

                    rabbitMqQueueInitializerChannel.CreateChannelAsync()
                        .Returns(Substitute.For<IChannel>());

                    services.AddSingleton(createdConsolidationPublisherChannel);
                    services.AddSingleton(rabbitMqQueueInitializerChannel);
                    services.AddSingleton(createdTransactionConsumerChannel);

                });

            }).CreateClient();

            var request = new DailyConsolidationRequest
            {
                AccountId = Guid.NewGuid(),
                StartDate = DateTime.UtcNow.Date,
                EndDate = DateTime.UtcNow.Date
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/consolidation/daily/query", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var result = await response.Content.ReadFromJsonAsync<DailyConsolidationResponse>();
            result.Should().NotBeNull();
        }
    }
}
