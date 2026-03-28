using System.Text;
using Clywell.Core.Messaging.RabbitMq;
using Microsoft.Extensions.Logging;
using Moq;
using RabbitMQ.Client;

namespace Clywell.Core.Messaging.Tests;

public sealed class RabbitMqPublisherTests
{
    [Fact]
    public async Task PublishAsync_CreatesChannel_DeclaresExchange_AndPublishesExpectedPayload()
    {
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "Sample.EventType",
            Payload = "{\"id\":1}",
            Exchange = "core.exchange",
            RoutingKey = "sample.created",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var expectedBody = Encoding.UTF8.GetBytes(message.Payload);
        var token = new CancellationTokenSource().Token;

        var channelMock = new Mock<IChannel>(MockBehavior.Strict);
        channelMock
            .Setup(c => c.ExchangeDeclareAsync(
                message.Exchange,
                ExchangeType.Topic,
                true,
                false,
                It.IsAny<IDictionary<string, object?>>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                token))
            .Returns(Task.CompletedTask)
            .Verifiable();

        channelMock
            .Setup(c => c.BasicPublishAsync(
                message.Exchange,
                message.RoutingKey,
                false,
                It.Is<BasicProperties>(p =>
                    p.Persistent
                    && p.MessageId == message.Id.ToString()
                    && p.ContentType == "application/json"
                    && p.Type == message.EventType),
                It.Is<ReadOnlyMemory<byte>>(body => body.ToArray().SequenceEqual(expectedBody)),
                token))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        channelMock
            .Setup(c => c.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        var connectionMock = new Mock<IConnection>(MockBehavior.Strict);
        connectionMock
            .Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), token))
            .ReturnsAsync(channelMock.Object)
            .Verifiable();

        var loggerMock = new Mock<ILogger<RabbitMqPublisher>>();
        var publisher = new RabbitMqPublisher(connectionMock.Object, loggerMock.Object);

        await publisher.PublishAsync(message, token);

        connectionMock.Verify();
        channelMock.Verify();
    }
}
