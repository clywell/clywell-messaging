# Changelog

All notable changes to the Clywell.Core.Messaging packages will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.2.0] - 2026-03-30

### Fixed

#### `Clywell.Core.Messaging.RabbitMq`
- `RabbitMqConsumerHostedService` now retries the connection with exponential backoff (5 s → 60 s cap) instead of throwing on startup, preventing a transient broker unavailability from crashing the host (previously `BackgroundServiceExceptionBehavior.StopHost` would terminate the process).

## [1.1.0] - 2026-03-30

### Fixed

#### `Clywell.Core.Messaging.RabbitMq`
- `AddRabbitMqConsumer` now binds `RabbitMqOptions` from configuration automatically, so `RabbitMq__*` environment variables are resolved without requiring a separate `PostConfigure` call in the host.
- Messages with no registered handler are now nacked without requeue (routed to the dead-letter exchange) instead of being silently acknowledged and lost.
- Wrapped the `IConnection` factory in `Task.Run` to prevent potential deadlocks when there is an ambient `SynchronizationContext` at DI container build time.

## [1.0.0] - 2026-03-23

Initial release of the Clywell.Core.Messaging package family, implementing the transactional outbox pattern and RabbitMQ transport for cross-service integration events.

### Added

#### `Clywell.Core.Messaging`

**Abstractions**
- `IDomainEventHandler<T>` — in-process handler interface. Implementations translate a domain event into one or more integration events and publish them via `IIntegrationEventPublisher`.
- `IIntegrationEventHandler<T>` — consumer-side handler interface. Implementations live in the receiving service and translate an integration event into a local command or notification.
- `IIntegrationEventPublisher` — queues an `IIntegrationEvent` for outbox insertion within the current database transaction. Called from `IDomainEventHandler<T>` implementations.
- `IRabbitMqPublisher` — publishes a single `OutboxMessage` to a RabbitMQ exchange and routing key.

**Outbox**
- `OutboxMessage` — outbox record entity. Fields: `Id` (Version 7 UUID), `EventType`, `Payload` (JSON), `Exchange`, `RoutingKey`, `CreatedAt`, `ProcessedAt`, `RetryCount`, `Error`.
- `OutboxMessageConfiguration` — EF Core `IEntityTypeConfiguration<OutboxMessage>` mapping to the `outbox_messages` table with a partial index on `processed_at IS NULL`.
- `ModelBuilderExtensions.AddOutboxMessages()` — applies `OutboxMessageConfiguration` to a `ModelBuilder`.

**EF Core Interceptor**
- `OutboxSaveChangesInterceptor` — singleton `SaveChangesInterceptor`. Intercepts `SavingChangesAsync` (pre-commit): drains domain events from all `IHasDomainEvents` entities via `IDomainEventDispatcher`, collects integration events published to `IIntegrationEventPublisher` (scoped `IntegrationEventCollector`), and inserts them as `OutboxMessage` rows within the same database transaction. Routing keys are auto-derived from the event type name (e.g. `UserRegisteredIntegrationEvent` → `user.registered`).
- `DbContextOptionsBuilderExtensions.UseOutboxInterceptor(IServiceProvider)` — wires `OutboxSaveChangesInterceptor` onto a `DbContextOptionsBuilder`.

**Background Processor**
- `OutboxProcessorService<TDbContext>` — generic `BackgroundService` that polls `outbox_messages` on a configurable interval using `FOR UPDATE SKIP LOCKED` (safe for multiple replicas). Forwards each message to `IRabbitMqPublisher`. Marks messages as processed on success; tracks retry count on failure and marks exhausted messages as processed-with-error to prevent infinite loops.

**Configuration**
- `MessagingOptions` — configures `Exchange`, `ProcessingInterval` (default 5 s), `BatchSize` (default 50), and `MaxRetryAttempts` (default 3). Bound from `appsettings.json` section `Messaging`.

**DI Registration**
- `ServiceCollectionExtensions.AddMessaging(configure?)` — registers `OutboxSaveChangesInterceptor` (singleton), `IntegrationEventCollector` (scoped), `IIntegrationEventPublisher` (scoped, backed by collector), `IDomainEventDispatcher` (scoped, backed by `DomainEventDispatcher`), and `MessagingOptions`.
- `ServiceCollectionExtensions.AddOutboxProcessor<TDbContext>()` — registers `OutboxProcessorService<TDbContext>` as a hosted service.
- `ServiceCollectionExtensions.AddDomainEventHandler<TEvent, THandler>()` — registers a scoped `IDomainEventHandler<T>` implementation. Multiple handlers per event type are supported.

---

#### `Clywell.Core.Messaging.RabbitMq`

**Transport**
- `RabbitMqPublisher` — `IRabbitMqPublisher` implementation. Opens a fresh channel per publish, declares the target exchange as a durable topic exchange, and publishes the message with `Persistent = true`, `MessageId`, `ContentType = application/json`, and `Type` set to the assembly-qualified event type name.
- `RabbitMqConsumerHostedService` — `BackgroundService` that declares the consumer queue (with optional dead-letter exchange binding), binds it to the configured exchange with routing key `#`, and dispatches received messages to all registered `IIntegrationEventHandler<T>` implementations via a new DI scope per message. Nacks without requeue on unknown type, deserialisation failure, or handler exception.

**Configuration**
- `RabbitMqOptions` — configures `Host`, `Port` (default 5672), `VirtualHost`, `Username`, and `Password`. Bound from section `RabbitMq`.
- `RabbitMqConsumerOptions` — configures `Exchange`, `QueueName`, `DeadLetterExchange`, `DeadLetterQueue`, and `PrefetchCount` (default 10). Bound from section `RabbitMqConsumer`.

**DI Registration**
- `ServiceCollectionExtensions.AddRabbitMqPublisher(configure?)` — registers singleton `IConnection` and scoped `IRabbitMqPublisher`.
- `ServiceCollectionExtensions.AddRabbitMqConsumer(configure?)` — registers singleton `IConnectionFactory` and `RabbitMqConsumerHostedService`.
- `ServiceCollectionExtensions.AddIntegrationEventHandler<TEvent, THandler>()` — registers a scoped `IIntegrationEventHandler<T>` implementation.

[Unreleased]: https://github.com/clywell/clywell-messaging/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/clywell/clywell-messaging/releases/tag/v1.0.0
