using EventStore.Client;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using NSubstitute;
using RabbitMQ.Client;
using Transaction.Tests.Unit.Common;
using TransactionService.Application.Commands;
using TransactionService.Infrastructure.EventStore;
using TransactionService.Infrastructure.Messaging.Channels;
using TransactionService.Infrastructure.Projections;

namespace Transaction.Tests.Unit.Application.Commands;

public class CreateTransactionCommandHandlerTests
{
    private readonly IEventStoreWrapper _eventStore = Substitute.For<IEventStoreWrapper>();
    private readonly IChannelPool _channelPool = Substitute.For<IChannelPool>();
    private readonly IMongoCollection<TransactionProjection> _mongoCollection = Substitute.For<IMongoCollection<TransactionProjection>>();
    private readonly ILogger<CreateTransactionCommandHandler> _logger = Substitute.For<ILogger<CreateTransactionCommandHandler>>();
    private readonly IChannel _channel = Substitute.For<IChannel>();

    private readonly CreateTransactionCommandHandler _handler;

    public CreateTransactionCommandHandlerTests()
    {
        var lease = new ChannelLease(_channelPool, _channel);

        _channelPool.RentAsync(Arg.Any<CancellationToken>()).Returns(lease);

        _handler = new CreateTransactionCommandHandler(
            _eventStore,
            _channelPool,
            _mongoCollection,
            _logger);
    }

    [Fact]
    public async Task HandleAsync_Success_ReturnsId()
    {
        // Arrange
        var command = new CreateTransactionCommand(Guid.NewGuid(), 150m);

        var updateResult = new UpdateResultTest();

        _mongoCollection.UpdateOneAsync(
            Arg.Any<FilterDefinition<TransactionProjection>>(),
            Arg.Any<UpdateDefinition<TransactionProjection>>(),
            Arg.Any<UpdateOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(await Task.FromResult(updateResult));


        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert

        await _eventStore.Received(1).AppendToStreamAsync(
            Arg.Any<string>(),
            StreamState.Any,
            Arg.Any<EventData[]>(),
            cancellationToken: Arg.Any<CancellationToken>());

        await _channel.Received(1).BasicPublishAsync(
            "",
            "transaction-created",
            true,
            Arg.Any<BasicProperties>(),
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Any<CancellationToken>());

        await _mongoCollection.Received(1).UpdateOneAsync(
            Arg.Any<FilterDefinition<TransactionProjection>>(),
            Arg.Any<UpdateDefinition<TransactionProjection>>(),
            Arg.Any<UpdateOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_Exception_PublishesToDLQ_AndThrows()
    {
        // Arrange
        var command = new CreateTransactionCommand(Guid.NewGuid(), 150m);

        _mongoCollection.UpdateOneAsync(
            Arg.Any<FilterDefinition<TransactionProjection>>(),
            Arg.Any<UpdateDefinition<TransactionProjection>>(),
            Arg.Any<UpdateOptions>(),
            Arg.Any<CancellationToken>()).Returns<Task<UpdateResult>>(x => throw new Exception("Test failure"));


        // Act & Assert
        var ex = await Assert.ThrowsAsync<Exception>(() => _handler.HandleAsync(command, CancellationToken.None));

        Assert.Equal("Test failure", ex.Message);

        await _channel.Received(1).BasicPublishAsync(
            "",
            "transaction-dlq",
            true,
            Arg.Any<BasicProperties>(),
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Any<CancellationToken>());
    }
}
