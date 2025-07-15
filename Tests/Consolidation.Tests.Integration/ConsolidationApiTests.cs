using System.Net;
using System.Net.Http.Json;
using ConsolidationService;
using ConsolidationService.Infrastructure.Projections;
using ConsolidationService.Presentation.Dtos.Request;
using ConsolidationService.Presentation.Dtos.Response;
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

                    var connection = Substitute.For<IConnection>();

                    var connectionFactory = Substitute.For<IConnectionFactory>();

                    services.AddSingleton(connection);
                    services.AddSingleton(connectionFactory);

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
