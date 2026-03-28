using Clywell.Primitives;

namespace Clywell.Core.Messaging.Tests;

public sealed class IntegrationEventCollectorTests
{
    [Fact]
    public async Task PublishAsync_AddsEventToCollected()
    {
        var collector = new IntegrationEventCollector();
        var evt = new TestIntegrationEvent();

        await collector.PublishAsync(evt);

        Assert.Single(collector.Collected);
        Assert.Same(evt, collector.Collected[0]);
    }

    [Fact]
    public async Task Clear_RemovesAllCollectedEvents()
    {
        var collector = new IntegrationEventCollector();

        await collector.PublishAsync(new TestIntegrationEvent());
        await collector.PublishAsync(new TestIntegrationEvent());

        collector.Clear();

        Assert.Empty(collector.Collected);
    }

    [Fact]
    public async Task Collected_ReturnsReadOnlyCollection()
    {
        var collector = new IntegrationEventCollector();
        await collector.PublishAsync(new TestIntegrationEvent());

        var collection = (ICollection<IIntegrationEvent>)collector.Collected;

        Assert.True(collection.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => collection.Add(new TestIntegrationEvent()));
    }

    private sealed class TestIntegrationEvent : IIntegrationEvent;
}
