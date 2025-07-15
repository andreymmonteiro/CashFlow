using System.Net;
using System.Net.Http.Json;
using BalanceService;
using BalanceService.Infrastructure.Projections;
using BalanceService.Presentation.Dtos.Response;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using NSubstitute;
using RabbitMQ.Client;

namespace Balance.Tests.Integration
{
    public class BalanceApiTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public BalanceApiTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task GetBalance_ByAccountId_ReturnsBalance()
        {
            // Arrange
            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");

                builder.ConfigureServices(services =>
                {
                    var mongoClient = Substitute.For<IMongoClient>();

                    var database = Substitute.For<IMongoDatabase>();

                    var collection = Substitute.For<IMongoCollection<BalanceProjection>>();

                    services.AddSingleton(collection);

                    var connection = Substitute.For<IConnection>();

                    var connectionFactory = Substitute.For<IConnectionFactory>();

                    services.AddSingleton(connection);
                    services.AddSingleton(connectionFactory);

                });

            }).CreateClient();

            // Act
            var response = await client.GetAsync($"api/balance?accountId={Guid.NewGuid().ToString()}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var result = await response.Content.ReadFromJsonAsync<BalanceResponse>();
            result.Should().NotBeNull();
        }
    }
}
