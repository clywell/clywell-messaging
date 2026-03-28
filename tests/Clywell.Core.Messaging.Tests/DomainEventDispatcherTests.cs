using Clywell.Primitives;
using Microsoft.Extensions.DependencyInjection;

namespace Clywell.Core.Messaging.Tests;

public sealed class DomainEventDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_DispatchesOnlyRegisteredEventTypes()
    {
        var handler = new RecordingHandler();

        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventHandler<HandledDomainEvent>>(handler);
        using var provider = services.BuildServiceProvider();

        var builder = new DomainEventHandlerRegistryBuilder();
        builder.Register<HandledDomainEvent>();
        var registry = new DomainEventDispatcherRegistry(builder);
        var dispatcher = new DomainEventDispatcher(registry, provider);

        IDomainEvent[] events = [new HandledDomainEvent(), new UnhandledDomainEvent()];

        await dispatcher.DispatchAsync(events);

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task DispatchAsync_ForwardsCancellationTokenToHandlers()
    {
        var handler = new RecordingHandler();

        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventHandler<HandledDomainEvent>>(handler);
        using var provider = services.BuildServiceProvider();

        var builder = new DomainEventHandlerRegistryBuilder();
        builder.Register<HandledDomainEvent>();
        var registry = new DomainEventDispatcherRegistry(builder);
        var dispatcher = new DomainEventDispatcher(registry, provider);

        var token = new CancellationTokenSource().Token;

        await dispatcher.DispatchAsync([new HandledDomainEvent()], token);

        Assert.Equal(token, handler.LastToken);
    }

    private sealed class HandledDomainEvent : IDomainEvent;

    private sealed class UnhandledDomainEvent : IDomainEvent;

    private sealed class RecordingHandler : IDomainEventHandler<HandledDomainEvent>
    {
        public int CallCount { get; private set; }
        public CancellationToken LastToken { get; private set; }

        public Task HandleAsync(HandledDomainEvent domainEvent, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastToken = cancellationToken;
            return Task.CompletedTask;
        }
    }
}
