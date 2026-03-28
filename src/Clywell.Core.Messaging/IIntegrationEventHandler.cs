using Clywell.Primitives;

namespace Clywell.Core.Messaging;

/// <summary>
/// Handles an integration event received from the message broker.
/// Implementations live in the consuming service and translate the event
/// into a local command or notification.
/// </summary>
/// <typeparam name="TIntegrationEvent">The integration event type this handler is responsible for.</typeparam>
public interface IIntegrationEventHandler<in TIntegrationEvent>
    where TIntegrationEvent : IIntegrationEvent
{
    /// <summary>Handles the specified integration event.</summary>
    Task HandleAsync(TIntegrationEvent integrationEvent, CancellationToken cancellationToken = default);
}
