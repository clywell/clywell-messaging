using Clywell.Core.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Clywell.Core.Messaging.RabbitMq;

/// <summary>
/// Extension methods for registering RabbitMQ transport services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers RabbitMQ publisher services:
    /// <list type="bullet">
    ///   <item>Singleton <see cref="IConnection"/> (shared across publishers)</item>
    ///   <item>Scoped <see cref="IRabbitMqPublisher"/> -&gt; <see cref="RabbitMqPublisher"/></item>
    /// </list>
    /// Reads connection settings from <c>appsettings.json</c> section <c>RabbitMq</c>.
    /// </summary>
    public static IServiceCollection AddRabbitMqPublisher(
        this IServiceCollection services,
        Action<RabbitMqOptions>? configure = null)
    {
        var optionsBuilder = services.AddOptions<RabbitMqOptions>()
            .BindConfiguration(RabbitMqOptions.SectionName);

        if (configure is not null)
            optionsBuilder.Configure(configure);

        services.AddSingleton<IConnection>(sp =>
        {
            var opt = sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
            var factory = new ConnectionFactory
            {
                HostName = opt.Host,
                Port = opt.Port,
                VirtualHost = opt.VirtualHost,
                UserName = opt.Username,
                Password = opt.Password,
            };
            // Task.Run avoids potential deadlocks when there is an ambient SynchronizationContext.
            return Task.Run(() => factory.CreateConnectionAsync()).GetAwaiter().GetResult();
        });

        services.AddScoped<IRabbitMqPublisher, RabbitMqPublisher>();
        return services;
    }

    /// <summary>
    /// Registers the <see cref="RabbitMqConsumerHostedService"/> and required options.
    /// </summary>
    public static IServiceCollection AddRabbitMqConsumer(
        this IServiceCollection services,
        Action<RabbitMqConsumerOptions>? configure = null)
    {
        var optionsBuilder = services.AddOptions<RabbitMqConsumerOptions>()
            .BindConfiguration(RabbitMqConsumerOptions.SectionName);

        if (configure is not null)
            optionsBuilder.Configure(configure);

        services.AddOptions<RabbitMqOptions>()
            .BindConfiguration(RabbitMqOptions.SectionName);

        // Register IConnectionFactory (used by consumer to create its own connection)
        services.AddSingleton<IConnectionFactory>(sp =>
        {
            var opt = sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
            return new ConnectionFactory
            {
                HostName = opt.Host,
                Port = opt.Port,
                VirtualHost = opt.VirtualHost,
                UserName = opt.Username,
                Password = opt.Password,
            };
        });

        services.AddHostedService<RabbitMqConsumerHostedService>();
        return services;
    }

    /// <summary>
    /// Registers an <see cref="IIntegrationEventHandler{T}"/> implementation.
    /// Multiple handlers per event type are supported.
    /// </summary>
    public static IServiceCollection AddIntegrationEventHandler<TEvent, THandler>(
        this IServiceCollection services)
        where TEvent : Clywell.Primitives.IIntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        services.AddScoped<IIntegrationEventHandler<TEvent>, THandler>();
        return services;
    }
}
