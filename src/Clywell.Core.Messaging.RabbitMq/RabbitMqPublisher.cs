using System.Text;
using Clywell.Core.Messaging;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Clywell.Core.Messaging.RabbitMq;

/// <summary>
/// Publishes outbox messages to RabbitMQ using a fresh channel per call.
/// Implements <see cref="IRabbitMqPublisher"/>.
/// </summary>
public sealed class RabbitMqPublisher(
    IConnection connection,
    ILogger<RabbitMqPublisher> logger)
    : IRabbitMqPublisher
{
    /// <inheritdoc />
    public async Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: message.Exchange,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        var properties = new BasicProperties
        {
            Persistent = true,
            MessageId = message.Id.ToString(),
            ContentType = "application/json",
            Type = message.EventType,
        };

        var body = Encoding.UTF8.GetBytes(message.Payload);

        await channel.BasicPublishAsync(
            exchange: message.Exchange,
            routingKey: message.RoutingKey,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);

        logger.LogDebug(
            "Published outbox message {MessageId} ({EventType}) to exchange '{Exchange}' with routing key '{RoutingKey}'.",
            message.Id, message.EventType, message.Exchange, message.RoutingKey);
    }
}
