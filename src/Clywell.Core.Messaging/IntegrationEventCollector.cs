using Clywell.Primitives;

namespace Clywell.Core.Messaging;

/// <summary>
/// Scoped, in-memory collector used by <see cref="OutboxSaveChangesInterceptor"/> to gather
/// integration events published by domain event handlers within a single save intercept cycle.
/// </summary>
internal sealed class IntegrationEventCollector : IIntegrationEventPublisher
{
    private readonly List<IIntegrationEvent> _events = [];

    /// <inheritdoc />
    public Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default)
        where T : IIntegrationEvent
    {
        _events.Add(@event);
        return Task.CompletedTask;
    }

    /// <summary>Gets all events collected since the last <see cref="Clear"/>.</summary>
    internal IReadOnlyList<IIntegrationEvent> Collected => _events.AsReadOnly();

    /// <summary>Clears all collected events.</summary>
    internal void Clear() => _events.Clear();
}
