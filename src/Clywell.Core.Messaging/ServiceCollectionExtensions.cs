using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Clywell.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Clywell.Core.Messaging;

/// <summary>
/// Extension methods for registering <c>Clywell.Core.Messaging</c> services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the core outbox infrastructure:
    /// <list type="bullet">
    ///   <item><see cref="OutboxSaveChangesInterceptor"/> (singleton)</item>
    ///   <item><see cref="IntegrationEventCollector"/> (scoped) and <see cref="IIntegrationEventPublisher"/> (scoped)</item>
    ///   <item><see cref="DomainEventDispatcher"/> as <see cref="Clywell.Core.Data.EntityFramework.IDomainEventDispatcher"/> (scoped)</item>
    ///   <item><see cref="DomainEventHandlerRegistryBuilder"/> (singleton, pre-created instance)</item>
    ///   <item><see cref="DomainEventDispatcherRegistry"/> (singleton, frozen at first resolution)</item>
    /// </list>
    /// Also configures <see cref="MessagingOptions"/> from <c>appsettings.json</c> section <c>Messaging</c>.
    /// </summary>
    public static IServiceCollection AddMessaging(
        this IServiceCollection services,
        Action<MessagingOptions>? configure = null)
    {
        var optionsBuilder = services.AddOptions<MessagingOptions>()
            .BindConfiguration(MessagingOptions.SectionName);

        if (configure is not null)
            optionsBuilder.Configure(configure);

        // Guard: only wire up once
        if (services.Any(d => d.ServiceType == typeof(DomainEventHandlerRegistryBuilder)))
            return services;

        // Pre-create the builder and register the same instance in DI so that
        // AddDomainEventHandler / AddDomainEventHandlersFromAssembly can populate it
        // before the container is built.
        var registryBuilder = new DomainEventHandlerRegistryBuilder();
        services.AddSingleton(registryBuilder);
        services.AddSingleton<DomainEventDispatcherRegistry>();
        services.AddScoped<Clywell.Core.Data.EntityFramework.IDomainEventDispatcher, DomainEventDispatcher>();

        services.AddSingleton<OutboxSaveChangesInterceptor>();
        services.AddScoped<IntegrationEventCollector>();
        services.AddScoped<IIntegrationEventPublisher>(sp =>
            sp.GetRequiredService<IntegrationEventCollector>());

        return services;
    }

    /// <summary>
    /// Registers <see cref="OutboxProcessorService{TDbContext}"/> as a hosted service.
    /// Call once per service, specifying the DbContext that owns the <c>outbox_messages</c> table.
    /// </summary>
    public static IServiceCollection AddOutboxProcessor<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        services.AddHostedService<OutboxProcessorService<TDbContext>>();
        return services;
    }

    /// <summary>
    /// Registers a single domain event handler.
    /// AOT-safe - all types are resolved at compile time.
    /// Multiple handlers for the same <typeparamref name="TEvent"/> are fully supported:
    /// all are invoked in registration order.
    /// </summary>
    public static IServiceCollection AddDomainEventHandler<TEvent, THandler>(
        this IServiceCollection services)
        where TEvent : IDomainEvent
        where THandler : class, IDomainEventHandler<TEvent>
    {
        services.AddScoped<IDomainEventHandler<TEvent>, THandler>();
        GetRegistryBuilder(services).Register<TEvent>();
        return services;
    }

    /// <summary>
    /// Scans <paramref name="assembly"/> and auto-registers every non-abstract
    /// <see cref="IDomainEventHandler{T}"/> implementation found.
    /// Equivalent to calling <see cref="AddDomainEventHandler{TEvent, THandler}"/> for each
    /// discovered handler, but without requiring a manual entry per type.
    /// </summary>
    /// <remarks>
    /// Assembly scanning uses reflection at startup. For NativeAOT publish, use the
    /// compile-time <see cref="AddDomainEventHandler{TEvent, THandler}"/> overload instead.
    /// </remarks>
    [RequiresUnreferencedCode(
        "Scans assemblies via reflection. For NativeAOT use AddDomainEventHandler<TEvent,THandler>() per handler.")]
    public static IServiceCollection AddDomainEventHandlersFromAssembly(
        this IServiceCollection services,
        Assembly assembly)
    {
        var registryBuilder = GetRegistryBuilder(services);
        var openHandlerInterface = typeof(IDomainEventHandler<>);

        var getDelegateMethod = typeof(ServiceCollectionExtensions)
            .GetMethod(nameof(GetDispatchDelegate), BindingFlags.NonPublic | BindingFlags.Static)!;

        var discovered = assembly
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == openHandlerInterface)
                .Select(i => (HandlerType: t, EventType: i.GetGenericArguments()[0])));

        foreach (var (handlerType, eventType) in discovered)
        {
            // Register the handler in DI (TryAddEnumerable avoids double-registration on repeated calls)
            services.TryAddEnumerable(
                ServiceDescriptor.Scoped(openHandlerInterface.MakeGenericType(eventType), handlerType));

            // Retrieve DispatchHelper<TEvent>.Delegate via a closed generic helper method.
            // MakeGenericMethod is called ONCE per handler type at startup - never at dispatch time.
            var dispatch = (Func<IDomainEvent, IServiceProvider, CancellationToken, Task>)
                getDelegateMethod.MakeGenericMethod(eventType).Invoke(null, null)!;

            registryBuilder.Register(eventType, dispatch);
        }

        return services;
    }

    /// <summary>
    /// Scans the assembly containing <typeparamref name="T"/> and auto-registers every
    /// non-abstract <see cref="IDomainEventHandler{T}"/> implementation found.
    /// </summary>
    [RequiresUnreferencedCode(
        "Scans assemblies via reflection. For NativeAOT use AddDomainEventHandler<TEvent,THandler>() per handler.")]
    public static IServiceCollection AddDomainEventHandlersFromAssemblyContaining<T>(
        this IServiceCollection services)
        => services.AddDomainEventHandlersFromAssembly(typeof(T).Assembly);

    // Invoked via MakeGenericMethod in the assembly scanner (once per handler type at startup).
    // Returns the static readonly delegate from DispatchHelper<TEvent> - AOT-safe.
    private static Func<IDomainEvent, IServiceProvider, CancellationToken, Task>
        GetDispatchDelegate<TEvent>() where TEvent : IDomainEvent
        => DispatchHelper<TEvent>.Delegate;

    private static DomainEventHandlerRegistryBuilder GetRegistryBuilder(IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(DomainEventHandlerRegistryBuilder));

        return descriptor?.ImplementationInstance as DomainEventHandlerRegistryBuilder
            ?? throw new InvalidOperationException(
                "Call services.AddMessaging() before registering domain event handlers.");
    }
}
