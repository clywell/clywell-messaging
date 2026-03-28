namespace Clywell.Core.Messaging.RabbitMq;

/// <summary>
/// Configuration for a RabbitMQ consumer queue binding.
/// </summary>
public sealed class RabbitMqConsumerOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "RabbitMqConsumer";

    /// <summary>Exchange to bind the consumer queue to.</summary>
    public string Exchange { get; set; } = string.Empty;

    /// <summary>Queue name this consumer declares and reads from.</summary>
    public string QueueName { get; set; } = string.Empty;

    /// <summary>Dead-letter exchange name.</summary>
    public string DeadLetterExchange { get; set; } = string.Empty;

    /// <summary>Dead-letter queue name.</summary>
    public string DeadLetterQueue { get; set; } = string.Empty;

    /// <summary>Number of unacknowledged messages per consumer. Default: <c>10</c>.</summary>
    public ushort PrefetchCount { get; set; } = 10;
}
