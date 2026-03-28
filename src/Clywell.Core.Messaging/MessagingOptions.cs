namespace Clywell.Core.Messaging;

/// <summary>
/// Configuration options for the transactional outbox processor.
/// </summary>
public sealed class MessagingOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Messaging";

    /// <summary>
    /// Default RabbitMQ exchange name used when publishing outbox messages.
    /// Can be overridden per event via routing key conventions.
    /// </summary>
    public string Exchange { get; set; } = string.Empty;

    /// <summary>How often the outbox processor polls for unprocessed messages. Default: 5 seconds.</summary>
    public TimeSpan ProcessingInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Maximum number of messages processed per polling cycle. Default: 50.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>Maximum publish retry attempts before a message is considered dead. Default: 3.</summary>
    public int MaxRetryAttempts { get; set; } = 3;
}
