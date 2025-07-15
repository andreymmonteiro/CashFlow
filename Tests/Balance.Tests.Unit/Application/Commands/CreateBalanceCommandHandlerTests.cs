using Balance.Tests.Unit.Common;
using BalanceService.Application.Commands;
using BalanceService.Infrastructure.EventStore;
using BalanceService.Infrastructure.Projections;
using EventStore.Client;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using NSubstitute;
using RabbitMQ.Client;

namespace Balance.Tests.Unit.Application.Commands;

public class CreateBalanceCommandHandlerTests
{
    private readonly IEventStoreWrapper _eventStore = Substitute.For<IEventStoreWrapper>();
    private readonly IConnection _connection = Substitute.For<IConnection>();
    private readonly IMongoCollection<BalanceProjection> _balances = Substitute.For<IMongoCollection<BalanceProjection>>();
    private readonly ILogger<CreateBalanceCommandHandler> _logger = Substitute.For<ILogger<CreateBalanceCommandHandler>>();
    private readonly IChannel _channel = Substitute.For<IChannel>();

    private readonly CreateBalanceCommandHandler _handler;

    public CreateBalanceCommandHandlerTests()
    {
        _connection.CreateChannelAsync(cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(_channel));

        _handler = new CreateBalanceCommandHandler(
            _eventStore,
            _connection,
            _balances,
            _logger);
    }

    [Fact]
    public async Task HandleAsync_Success_ReturnsModifiedCount()
    {
        // Arrange
        var command = new CreateBalanceCommand(Guid.NewGuid().ToString(), 100m, DateTime.UtcNow);

        var updateResult = new UpdateResultTest();

        _balances.UpdateOneAsync(
            Arg.Any<FilterDefinition<BalanceProjection>>(),
            Arg.Any<UpdateDefinition<BalanceProjection>>(),
            Arg.Any<UpdateOptions>(),
            Arg.Any<CancellationToken>())
        .Returns(await Task.FromResult(updateResult));

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.Equal(1, result);

        await _eventStore.Received(1).AppendToStreamAsync(
            Arg.Any<string>(),
            StreamState.Any,
            Arg.Any<IEnumerable<EventData>>(),
            cancellationToken: Arg.Any<CancellationToken>());

        await _channel.Received(1).BasicPublishAsync(
            "",
            "balance.created",
            true,
            Arg.Any<BasicProperties>(),
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_Failure_PublishesToDLQAndReturns0()
    {
        // Arrange
        var command = new CreateBalanceCommand(Guid.NewGuid().ToString(), 100m, DateTime.UtcNow);

        _balances.UpdateOneAsync(
            Arg.Any<FilterDefinition<BalanceProjection>>(),
            Arg.Any<UpdateDefinition<BalanceProjection>>(),
            Arg.Any<UpdateOptions>(),
            Arg.Any<CancellationToken>())
        .Returns<Task<UpdateResult>>(x => throw new Exception("Test exception"));

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.Equal(0, result);

        await _channel.Received(1).BasicPublishAsync(
            "",
            "consolidation.dlq",
            true,
            Arg.Any<BasicProperties>(),
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Any<CancellationToken>());
    }
}
