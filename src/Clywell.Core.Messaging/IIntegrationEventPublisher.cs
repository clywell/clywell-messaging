using Clywell.Primitives;

namespace Clywell.Core.Messaging;

/// <summary>
/// Publishes an integration event to the outbox within the current database transaction.
/// Called from <see cref="IDomainEventHandler{T}"/> implementations during the
/// <c>SavingChangesAsync</c> interception point - writes are atomic with the entity save.
/// </summary>
public interface IIntegrationEventPublisher
{
    /// <summary>
    /// Queues <paramref name="event"/> to be written to the outbox table.
    /// The actual database write occurs in the interceptor after all handlers complete.
    /// </summary>
    Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default)
        where T : IIntegrationEvent;
}
