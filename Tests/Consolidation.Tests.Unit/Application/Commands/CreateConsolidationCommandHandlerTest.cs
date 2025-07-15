using Consolidation.Tests.Unit.Common;
using ConsolidationService.Application.Commands;
using ConsolidationService.Infrastructure.EventStore;
using ConsolidationService.Infrastructure.Projections;
using EventStore.Client;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using NSubstitute;
using RabbitMQ.Client;

namespace Consolidation.Tests.Unit.Application.Commands;

public class CreateConsolidationCommandHandlerTests
{
    private readonly IEventStoreWrapper _eventStore = Substitute.For<IEventStoreWrapper>();
    private readonly IConnection _connection = Substitute.For<IConnection>();
    private readonly IMongoCollection<ConsolidationProjection> _consolidations = Substitute.For<IMongoCollection<ConsolidationProjection>>();
    private readonly ILogger<CreateConsolidationCommandHandler> _logger = Substitute.For<ILogger<CreateConsolidationCommandHandler>>();
    private readonly IChannel _channel = Substitute.For<IChannel>();

    private readonly CreateConsolidationCommandHandler _handler;

    public CreateConsolidationCommandHandlerTests()
    {
        _connection.CreateChannelAsync(cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(_channel));

        _handler = new CreateConsolidationCommandHandler(
            _eventStore,
            _connection,
            _consolidations,
            _logger);
    }

    [Fact]
    public async Task HandleAsync_Success_ReturnsModifiedCount()
    {
        // Arrange
        var command = new CreateConsolidationCommand(Guid.NewGuid().ToString(), 100m, DateTime.UtcNow);

        var upsertResult = new UpdateResultTest();

        _consolidations.UpdateOneAsync(
            Arg.Any<FilterDefinition<ConsolidationProjection>>(),
            Arg.Any<UpdateDefinition<ConsolidationProjection>>(),
            Arg.Any<UpdateOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(await Task.FromResult(upsertResult));

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.Equal(1, result);

        await _eventStore.Received(1).AppendToStreamAsync(
            Arg.Is<string>(s => s.StartsWith("consolidation-")),
            StreamState.Any,
            Arg.Any<EventData[]>(),
            cancellationToken: Arg.Any<CancellationToken>());

        await _channel.Received(1).BasicPublishAsync(
            "",
            "consolidation.created",
            true,
            Arg.Any<BasicProperties>(),
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Any<CancellationToken>());

        await _consolidations.Received(1).UpdateOneAsync(
            Arg.Any<FilterDefinition<ConsolidationProjection>>(),
            Arg.Any<UpdateDefinition<ConsolidationProjection>>(),
            Arg.Any<UpdateOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_Exception_PublishesToDLQ_ReturnsZero()
    {
        // Arrange
        var command = new CreateConsolidationCommand(Guid.NewGuid().ToString(), 100m, DateTime.UtcNow);

        _consolidations.UpdateOneAsync(
            Arg.Any<FilterDefinition<ConsolidationProjection>>(),
            Arg.Any<UpdateDefinition<ConsolidationProjection>>(),
            Arg.Any<UpdateOptions>(),
            Arg.Any<CancellationToken>())
            .Returns<Task<UpdateResult>>(x => throw new Exception("Test error"));

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
