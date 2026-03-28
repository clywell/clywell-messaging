using Clywell.Core.Data.EntityFramework;
using Clywell.Primitives;
using Microsoft.Extensions.DependencyInjection;

namespace Clywell.Core.Messaging.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddMessaging_RegistersExpectedServicesWithExpectedLifetimes()
    {
        var services = new ServiceCollection();

        services.AddMessaging();

        Assert.Contains(services, d =>
            d.ServiceType == typeof(DomainEventHandlerRegistryBuilder) && d.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, d =>
            d.ServiceType == typeof(DomainEventDispatcherRegistry) && d.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, d =>
            d.ServiceType == typeof(IDomainEventDispatcher)
            && d.ImplementationType == typeof(DomainEventDispatcher)
            && d.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, d =>
            d.ServiceType == typeof(OutboxSaveChangesInterceptor) && d.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, d =>
            d.ServiceType == typeof(IntegrationEventCollector) && d.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, d =>
            d.ServiceType == typeof(IIntegrationEventPublisher) && d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddMessaging_IsIdempotentForRegistryBuilderRegistration()
    {
        var services = new ServiceCollection();

        services.AddMessaging();
        services.AddMessaging();

        var builderRegistrations = services
            .Count(d => d.ServiceType == typeof(DomainEventHandlerRegistryBuilder));

        Assert.Equal(1, builderRegistrations);
    }

    [Fact]
    public void AddDomainEventHandler_RegistersHandlerAndEventTypeDispatcher()
    {
        var services = new ServiceCollection();
        services.AddMessaging();

        services.AddDomainEventHandler<TestDomainEvent, TestDomainEventHandler>();

        using var provider = services.BuildServiceProvider();
        var handlers = provider.GetServices<IDomainEventHandler<TestDomainEvent>>().ToList();

        Assert.Single(handlers);
        Assert.IsType<TestDomainEventHandler>(handlers[0]);

        var builderDescriptor = services.Single(d => d.ServiceType == typeof(DomainEventHandlerRegistryBuilder));
        var builder = Assert.IsType<DomainEventHandlerRegistryBuilder>(builderDescriptor.ImplementationInstance);

        var registry = new DomainEventDispatcherRegistry(builder);
        Assert.True(registry.TryGetDispatch(typeof(TestDomainEvent), out _));
    }

    private sealed class TestDomainEvent : IDomainEvent;

    private sealed class TestDomainEventHandler : IDomainEventHandler<TestDomainEvent>
    {
        public Task HandleAsync(TestDomainEvent domainEvent, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
