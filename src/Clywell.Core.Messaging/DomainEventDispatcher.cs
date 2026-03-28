using Clywell.Core.Data.EntityFramework;
using Clywell.Primitives;

namespace Clywell.Core.Messaging;

/// <summary>
/// Dispatches domain events to their handlers via a pre-built, frozen delegate registry.
/// </summary>
/// <remarks>
/// No <see cref="System.Type.MakeGenericType"/>, no <see cref="System.Reflection.MethodBase.Invoke(object?, object[])"/>
/// at dispatch time. AOT-compatible when handlers are registered via
/// <see cref="ServiceCollectionExtensions.AddDomainEventHandler{TEvent, THandler}"/>.
/// For assembly-scanning registration see
/// <see cref="ServiceCollectionExtensions.AddDomainEventHandlersFromAssembly"/>.
/// </remarks>
internal sealed class DomainEventDispatcher(
    DomainEventDispatcherRegistry registry,
    IServiceProvider serviceProvider) : IDomainEventDispatcher
{
    /// <inheritdoc />
    public async Task DispatchAsync(
        IReadOnlyList<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            if (registry.TryGetDispatch(domainEvent.GetType(), out var dispatch))
                await dispatch(domainEvent, serviceProvider, cancellationToken);
        }
    }
}
