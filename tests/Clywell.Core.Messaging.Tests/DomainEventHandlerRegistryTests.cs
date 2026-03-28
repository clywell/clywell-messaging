using Clywell.Primitives;
using Microsoft.Extensions.DependencyInjection;

namespace Clywell.Core.Messaging.Tests;

public sealed class DomainEventHandlerRegistryTests
{
    [Fact]
    public void Register_GenericEvent_AddsDispatcherToRegistry()
    {
        var builder = new DomainEventHandlerRegistryBuilder();
        builder.Register<TestDomainEvent>();

        var registry = new DomainEventDispatcherRegistry(builder);

        Assert.True(registry.TryGetDispatch(typeof(TestDomainEvent), out var dispatch));
        Assert.NotNull(dispatch);
    }

    [Fact]
    public async Task DispatchDelegate_InvokesAllHandlersFromServiceProvider()
    {
        var first = new RecordingHandler();
        var second = new RecordingHandler();

        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventHandler<TestDomainEvent>>(first);
        services.AddSingleton<IDomainEventHandler<TestDomainEvent>>(second);
        using var provider = services.BuildServiceProvider();

        var builder = new DomainEventHandlerRegistryBuilder();
        builder.Register<TestDomainEvent>();
        var registry = new DomainEventDispatcherRegistry(builder);

        var token = new CancellationTokenSource().Token;
        var evt = new TestDomainEvent();

        Assert.True(registry.TryGetDispatch(typeof(TestDomainEvent), out var dispatch));
        await dispatch(evt, provider, token);

        Assert.Equal(1, first.CallCount);
        Assert.Equal(1, second.CallCount);
        Assert.Equal(token, first.LastToken);
        Assert.Equal(token, second.LastToken);
    }

    private sealed class TestDomainEvent : IDomainEvent;

    private sealed class RecordingHandler : IDomainEventHandler<TestDomainEvent>
    {
        public int CallCount { get; private set; }
        public CancellationToken LastToken { get; private set; }

        public Task HandleAsync(TestDomainEvent domainEvent, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastToken = cancellationToken;
            return Task.CompletedTask;
        }
    }
}
