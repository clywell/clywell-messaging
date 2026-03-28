namespace Clywell.Core.Messaging.Tests;

public sealed class OutboxMessageTests
{
    [Fact]
    public void InitProperties_AreAssignedFromObjectInitializer()
    {
        var id = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;

        var message = new OutboxMessage
        {
            Id = id,
            EventType = "Sample.Event",
            Payload = "{\"value\":42}",
            Exchange = "events",
            RoutingKey = "sample.event",
            CreatedAt = createdAt,
        };

        Assert.Equal(id, message.Id);
        Assert.Equal("Sample.Event", message.EventType);
        Assert.Equal("{\"value\":42}", message.Payload);
        Assert.Equal("events", message.Exchange);
        Assert.Equal("sample.event", message.RoutingKey);
        Assert.Equal(createdAt, message.CreatedAt);
    }

    [Fact]
    public void MutableProperties_AreSettable()
    {
        var processedAt = DateTimeOffset.UtcNow;

        var message = new OutboxMessage
        {
            RetryCount = 2,
            Error = "publish failed",
            ProcessedAt = processedAt,
        };

        Assert.Equal(2, message.RetryCount);
        Assert.Equal("publish failed", message.Error);
        Assert.Equal(processedAt, message.ProcessedAt);
    }
}
