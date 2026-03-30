using System.Text;
using System.Text.Json;
using Clywell.Core.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Clywell.Core.Messaging.RabbitMq;

/// <summary>
/// Hosted service that consumes integration events from a RabbitMQ queue and dispatches them
/// to registered <see cref="IIntegrationEventHandler{T}"/> implementations.
/// Implements dead-letter handling: messages exceeding max retries are nacked without requeue.
/// </summary>
public sealed class RabbitMqConsumerHostedService(
    IConnectionFactory connectionFactory,
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqConsumerOptions> options,
    ILogger<RabbitMqConsumerHostedService> logger)
    : BackgroundService
{
    private IConnection? _connection;
    private IChannel? _channel;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _connection = await connectionFactory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        var opt = options.Value;

        // Declare dead-letter exchange and queue first
        if (!string.IsNullOrEmpty(opt.DeadLetterExchange))
        {
            await _channel.ExchangeDeclareAsync(
                exchange: opt.DeadLetterExchange,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: stoppingToken);

            if (!string.IsNullOrEmpty(opt.DeadLetterQueue))
            {
                await _channel.QueueDeclareAsync(
                    queue: opt.DeadLetterQueue,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    cancellationToken: stoppingToken);

                await _channel.QueueBindAsync(
                    queue: opt.DeadLetterQueue,
                    exchange: opt.DeadLetterExchange,
                    routingKey: "#",
                    cancellationToken: stoppingToken);
            }
        }

        // Declare primary exchange
        await _channel.ExchangeDeclareAsync(
            exchange: opt.Exchange,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken);

        // Build queue arguments
        Dictionary<string, object?>? queueArgs = null;
        if (!string.IsNullOrEmpty(opt.DeadLetterExchange))
        {
            queueArgs = new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = opt.DeadLetterExchange,
            };
        }

        // Declare the consumer queue
        await _channel.QueueDeclareAsync(
            queue: opt.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: queueArgs,
            cancellationToken: stoppingToken);

        await _channel.QueueBindAsync(
            queue: opt.QueueName,
            exchange: opt.Exchange,
            routingKey: "#",
            cancellationToken: stoppingToken);

        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: opt.PrefetchCount,
            global: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += (_, ea) => HandleMessageAsync(ea, stoppingToken);

        await _channel.BasicConsumeAsync(
            queue: opt.QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        // Keep alive until cancelled
        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
    }

    private async Task HandleMessageAsync(BasicDeliverEventArgs ea, CancellationToken cancellationToken)
    {
        var messageType = ea.BasicProperties.Type;

        try
        {
            var clrType = ResolveType(messageType);

            if (clrType is null)
            {
                logger.LogWarning(
                    "Received message with unknown event type '{EventType}'. Nacking without requeue.",
                    messageType);
                await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken);
                return;
            }

            var payload = Encoding.UTF8.GetString(ea.Body.Span);
            var integrationEvent = JsonSerializer.Deserialize(payload, clrType);

            if (integrationEvent is null)
            {
                logger.LogWarning(
                    "Failed to deserialise message of type '{EventType}'. Nacking without requeue.",
                    messageType);
                await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken);
                return;
            }

            using var scope = scopeFactory.CreateScope();
            var handlerType = typeof(IIntegrationEventHandler<>).MakeGenericType(clrType);
            var handlers = scope.ServiceProvider.GetServices(handlerType).ToList();

            if (handlers.Count == 0)
            {
                logger.LogWarning(
                    "No handler registered for integration event '{EventType}'. Nacking without requeue.",
                    messageType);
                await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken);
                return;
            }

            foreach (var handler in handlers)
            {
                var handleMethod = handlerType.GetMethod(nameof(IIntegrationEventHandler<Clywell.Primitives.IIntegrationEvent>.HandleAsync))!;
                await (Task)handleMethod.Invoke(handler, [integrationEvent, cancellationToken])!;
            }

            await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling integration event of type '{EventType}'.", messageType);
            await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken);
        }
    }

    private static Type? ResolveType(string? typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return null;

        var type = Type.GetType(typeName);
        if (type is not null)
            return type;

        // Fallback: scan loaded assemblies by full name
        return AppDomain.CurrentDomain
            .GetAssemblies()
            .Select(a => a.GetType(typeName))
            .FirstOrDefault(t => t is not null);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}
