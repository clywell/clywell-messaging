namespace Clywell.Core.Messaging;

/// <summary>
/// Publishes a single outbox message to a RabbitMQ exchange.
/// </summary>
public interface IRabbitMqPublisher
{
    /// <summary>Publishes <paramref name="message"/> to its configured exchange and routing key.</summary>
    Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}
