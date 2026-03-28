namespace Clywell.Core.Messaging;

/// <summary>
/// Represents a pending outbox record. Added to the outbox table within the same
/// database transaction as the entity change, then processed asynchronously by
/// <c>OutboxProcessorService</c> which forwards it to the message broker.
/// </summary>
public sealed class OutboxMessage
{
    /// <summary>Unique message identifier (Version 7 UUID for sortability).</summary>
    public Guid Id { get; init; }

    /// <summary>Assembly-qualified name of the integration event CLR type.</summary>
    public string EventType { get; init; } = string.Empty;

    /// <summary>JSON-serialised integration event payload.</summary>
    public string Payload { get; init; } = string.Empty;

    /// <summary>RabbitMQ exchange name to publish to.</summary>
    public string Exchange { get; init; } = string.Empty;

    /// <summary>RabbitMQ routing key.</summary>
    public string RoutingKey { get; init; } = string.Empty;

    /// <summary>UTC timestamp when the message was written to the outbox.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>UTC timestamp when the message was successfully published to the broker. Null = unprocessed.</summary>
    public DateTimeOffset? ProcessedAt { get; set; }

    /// <summary>Number of publish attempts so far.</summary>
    public int RetryCount { get; set; }

    /// <summary>Last error message from a failed publish attempt, if any.</summary>
    public string? Error { get; set; }
}
