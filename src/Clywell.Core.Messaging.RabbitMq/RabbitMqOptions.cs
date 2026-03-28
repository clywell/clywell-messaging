namespace Clywell.Core.Messaging.RabbitMq;

/// <summary>
/// Configuration options for the RabbitMQ connection and topology.
/// </summary>
public sealed class RabbitMqOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "RabbitMq";

    /// <summary>RabbitMQ host name. Default: <c>localhost</c>.</summary>
    public string Host { get; set; } = "localhost";

    /// <summary>AMQP port. Default: <c>5672</c>.</summary>
    public int Port { get; set; } = 5672;

    /// <summary>Virtual host. Default: <c>/</c>.</summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>RabbitMQ username. Required — must be set via configuration.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>RabbitMQ password. Required — must be set via configuration.</summary>
    public string Password { get; set; } = string.Empty;
}
