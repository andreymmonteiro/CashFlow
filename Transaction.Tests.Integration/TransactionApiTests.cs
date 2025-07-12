using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using NSubstitute;
using RabbitMQ.Client;
using TransactionService;
using TransactionService.Application.Commands;
using TransactionService.Infrastructure.EventStore;
using TransactionService.Infrastructure.Projections;
using TransactionService.Presentation.Dtos.Request;
using TransactionService.Presentation.Dtos.Response;

namespace Transaction.Tests.Integration
{
    public class TransactionApiTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public TransactionApiTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task CreateTransaction_Returns_Created()
        {
            // Arrange

            var eventStore = Substitute.For<IEventStoreWrapper>();

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");

                builder.ConfigureServices(services =>
                {
                    var mongoClient = Substitute.For<IMongoClient>();

                    var database = Substitute.For<IMongoDatabase>();

                    var collection = Substitute.For<IMongoCollection<TransactionProjection>>();

                    services.AddSingleton(collection);

                    var channel = Substitute.For<IChannel>();

                    services.AddSingleton(channel);

                    services.AddSingleton(eventStore);

                });

            }).CreateClient();

            var request = new CreateTransactionCommand(Guid.NewGuid(), 100.50m);

            // Act
            var response = await client.PostAsJsonAsync("api/transactions", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var id = await response.Content.ReadFromJsonAsync<Guid>();
            id.Should().NotBeEmpty();
        }

        [Fact]
        public async Task GetTransactions_ByAccountIdAndRAngeDate_ReturnsTransactions()
        {
            // Arrange
            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");

                builder.ConfigureServices(services =>
                {
                    var mongoClient = Substitute.For<IMongoClient>();

                    var database = Substitute.For<IMongoDatabase>();

                    var collection = Substitute.For<IMongoCollection<TransactionProjection>>();

                    services.AddSingleton(collection);

                    var channel = Substitute.For<IChannel>();

                    services.AddSingleton(channel);

                });

            }).CreateClient();

            var request = new DailyTransactionRequest
            {
                AccountId = Guid.NewGuid(),
                StartDate = DateTime.UtcNow.Date,
                EndDate = DateTime.UtcNow.Date
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/account/transactions/query", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var result = await response.Content.ReadFromJsonAsync<DailyTransactionResponse>();
            result.Should().NotBeNull();
        }
    }
}
