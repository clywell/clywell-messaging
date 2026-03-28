using System.Collections.Frozen;
using Clywell.Primitives;
using Microsoft.Extensions.DependencyInjection;

namespace Clywell.Core.Messaging;

/// <summary>
/// Mutable builder populated at DI registration time.
/// Frozen into a <see cref="DomainEventDispatcherRegistry"/> when the container is first used.
/// </summary>
/// <remarks>
/// A single pre-created instance is registered in the DI container as a singleton so that
/// <see cref="ServiceCollectionExtensions.AddDomainEventHandler{TEvent, THandler}"/> and the
/// assembly-scanning overloads can populate it before the container is built.
/// </remarks>
internal sealed class DomainEventHandlerRegistryBuilder
{
    private readonly Dictionary<Type, Func<IDomainEvent, IServiceProvider, CancellationToken, Task>> _dispatchers = [];

    /// <summary>
    /// Registers the AOT-safe dispatch delegate for <typeparamref name="TEvent"/>.
    /// Idempotent: multiple handlers for the same event type share one delegate because
    /// <see cref="DispatchHelper{TEvent}.Delegate"/> calls
    /// <c>GetServices&lt;IDomainEventHandler&lt;TEvent&gt;&gt;</c> which returns all of them.
    /// </summary>
    internal void Register<TEvent>() where TEvent : IDomainEvent
        => _dispatchers.TryAdd(typeof(TEvent), DispatchHelper<TEvent>.Delegate);

    /// <summary>
    /// Non-generic overload used by the assembly scanner.
    /// The <paramref name="dispatch"/> delegate must come from
    /// <see cref="DispatchHelper{TEvent}.Delegate"/> to preserve AOT-safety.
    /// </summary>
    internal void Register(Type eventType, Func<IDomainEvent, IServiceProvider, CancellationToken, Task> dispatch)
        => _dispatchers.TryAdd(eventType, dispatch);

    internal FrozenDictionary<Type, Func<IDomainEvent, IServiceProvider, CancellationToken, Task>> Build()
        => _dispatchers.ToFrozenDictionary();
}

/// <summary>
/// Holds a single cached, AOT-safe dispatch delegate per <typeparamref name="TEvent"/>.
/// The <c>static readonly</c> field is initialised once per <typeparamref name="TEvent"/> by
/// the CLR. The <c>static</c> lambda prevents closure capture; <typeparamref name="TEvent"/>
/// is bound in the generic parameter - no heap allocation at dispatch time.
/// </summary>
internal static class DispatchHelper<TEvent> where TEvent : IDomainEvent
{
    internal static readonly Func<IDomainEvent, IServiceProvider, CancellationToken, Task> Delegate =
        static async (evt, sp, ct) =>
        {
            foreach (var handler in sp.GetServices<IDomainEventHandler<TEvent>>())
                await handler.HandleAsync((TEvent)evt, ct);
        };
}