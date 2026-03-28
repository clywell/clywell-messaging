using Clywell.Primitives;

namespace Clywell.Core.Messaging;

/// <summary>
/// Handles an in-process domain event raised by a domain entity.
/// Implementations typically translate the domain event into an integration event
/// and publish it via <see cref="IIntegrationEventPublisher"/>.
/// </summary>
/// <typeparam name="TDomainEvent">The domain event type this handler is responsible for.</typeparam>
public interface IDomainEventHandler<in TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    /// <summary>Handles the specified domain event.</summary>
    Task HandleAsync(TDomainEvent domainEvent, CancellationToken cancellationToken = default);
}
