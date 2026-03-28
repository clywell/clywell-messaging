using System.Collections.Frozen;
using Clywell.Primitives;

namespace Clywell.Core.Messaging;

/// <summary>
/// Immutable, frozen registry of dispatch delegates built once when the DI container is first used.
/// Injected into <see cref="DomainEventDispatcher"/> as a singleton.
/// </summary>
internal sealed class DomainEventDispatcherRegistry(DomainEventHandlerRegistryBuilder builder)
{
    private readonly FrozenDictionary<Type, Func<IDomainEvent, IServiceProvider, CancellationToken, Task>> _handlers = builder.Build();

    /// <summary>
    /// Tries to find the pre-compiled dispatch delegate for <paramref name="eventType"/>.
    /// Returns <see langword="false"/> if no handler was registered for that event type.
    /// </summary>
    public bool TryGetDispatch(
        Type eventType,
        out Func<IDomainEvent, IServiceProvider, CancellationToken, Task> dispatch)
        => _handlers.TryGetValue(eventType, out dispatch!);
}
