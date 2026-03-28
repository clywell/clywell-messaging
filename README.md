# Clywell.Core.Messaging

[![Build Status](https://github.com/clywell/clywell-messaging/actions/workflows/ci-cd.yml/badge.svg)](https://github.com/clywell/clywell-messaging/actions/workflows/ci-cd.yml)
[![NuGet Version](https://img.shields.io/nuget/v/Clywell.Core.Messaging.svg)](https://www.nuget.org/packages/Clywell.Core.Messaging/)
[![License](https://img.shields.io/github/license/clywell/clywell-messaging.svg)](LICENSE)

Transactional outbox messaging for .NET — reliable cross-service integration events with EF Core outbox infrastructure and RabbitMQ transport.

---

## Packages

| Package | NuGet | Description |
|---------|-------|----------|
| `Clywell.Core.Messaging` | [![NuGet](https://img.shields.io/nuget/v/Clywell.Core.Messaging.svg)](https://www.nuget.org/packages/Clywell.Core.Messaging/) | Core abstractions, transactional outbox infrastructure, EF Core interceptor, and background processor |
| `Clywell.Core.Messaging.RabbitMq` | [![NuGet](https://img.shields.io/nuget/v/Clywell.Core.Messaging.RabbitMq.svg)](https://www.nuget.org/packages/Clywell.Core.Messaging.RabbitMq/) | RabbitMQ transport provider with publisher and consumer hosted service |

---

## Architecture

1. A domain entity raises a domain event implementing `IDomainEvent` (`Clywell.Primitives`).
2. `OutboxSaveChangesInterceptor` intercepts `SaveChangesAsync`, drains domain events, and dispatches them to `IDomainEventHandler<T>` implementations.
3. `IDomainEventHandler<T>` translates domain events into `IIntegrationEvent` instances and publishes via `IIntegrationEventPublisher`.
4. The interceptor inserts each integration event as an `OutboxMessage` row in the same database transaction.
5. `OutboxProcessorService<TDbContext>` polls the outbox on a configurable interval and forwards each message to `IRabbitMqPublisher`.
6. `RabbitMqConsumerHostedService` receives messages and dispatches to `IIntegrationEventHandler<T>` implementations in receiving services.

Routing keys are derived automatically from event type names. Example: `UserRegisteredIntegrationEvent` -> `user.registered`.

---

## Interfaces

### `Clywell.Core.Messaging`

| Interface | Description |
|----------|-------------|
| `IDomainEventHandler<TDomainEvent>` | Handles an in-process domain event; translates it into integration event(s) and publishes through `IIntegrationEventPublisher` |
| `IIntegrationEventHandler<TIntegrationEvent>` | Consumer-side handler; translates integration events into local commands or notifications |
| `IIntegrationEventPublisher` | Queues an `IIntegrationEvent` for outbox insertion within the current DB transaction |
| `IRabbitMqPublisher` | Publishes a single `OutboxMessage` to RabbitMQ (implemented by the RabbitMQ package) |

---

## Installation

Install both packages on publisher and consumer services:

```bash
dotnet add package Clywell.Core.Messaging
dotnet add package Clywell.Core.Messaging.RabbitMq
```

---

## Quick Start

### Publisher side setup

#### 1. Apply the outbox table to your `DbContext`

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // ...existing configuration...
    modelBuilder.AddOutboxMessages(); // from Clywell.Core.Messaging
}
```

#### 2. Wire the outbox interceptor onto your `DbContext`

```csharp
// In Program.cs / service registration
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    options.UseNpgsql(connectionString)
           .UseOutboxInterceptor(sp); // from Clywell.Core.Messaging
});
```

#### 3. Register messaging services

```csharp
// Core outbox infrastructure
builder.Services.AddMessaging(options =>
{
    options.Exchange = "my-app.events";
    options.ProcessingInterval = TimeSpan.FromSeconds(5);
    options.BatchSize = 50;
    options.MaxRetryAttempts = 3;
});

// RabbitMQ publisher
builder.Services.AddRabbitMqPublisher(options =>
{
    options.Host = "localhost";
    options.Port = 5672;
    options.VirtualHost = "/";
    options.Username = "guest";
    options.Password = "guest";
});

// Outbox background processor
builder.Services.AddOutboxProcessor<AppDbContext>();

// Register domain event handlers (AOT-safe, compile-time registration)
builder.Services.AddDomainEventHandler<UserRegisteredDomainEvent, UserRegisteredDomainEventHandler>();

// Or scan an assembly (reflection-based, not NativeAOT-safe)
builder.Services.AddDomainEventHandlersFromAssembly(typeof(Program).Assembly);
// or: AddDomainEventHandlersFromAssemblyContaining<T>()
```

#### 4. Implement a domain event handler

```csharp
public class UserRegisteredDomainEventHandler(IIntegrationEventPublisher publisher)
    : IDomainEventHandler<UserRegisteredDomainEvent>
{
    public async Task HandleAsync(UserRegisteredDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        await publisher.PublishAsync(new UserRegisteredIntegrationEvent
        {
            UserId = domainEvent.UserId,
            Email = domainEvent.Email
        }, cancellationToken);
    }
}
```

### Consumer side setup

#### 1. Register messaging services

```csharp
// Core abstractions
builder.Services.AddMessaging();

// RabbitMQ connection (required by consumer)
builder.Services.AddRabbitMqPublisher(options =>
{
    options.Host = "localhost";
    options.Username = "guest";
    options.Password = "guest";
});

// Consumer hosted service
builder.Services.AddRabbitMqConsumer(options =>
{
    options.Exchange = "my-app.events";
    options.QueueName = "my-consumer-service";
    options.DeadLetterExchange = "my-app.events.dlx";
    options.DeadLetterQueue = "my-consumer-service.dlq";
    options.PrefetchCount = 10;
});

// Register integration event handlers
builder.Services.AddIntegrationEventHandler<UserRegisteredIntegrationEvent, UserRegisteredIntegrationEventHandler>();
```

#### 2. Implement an integration event handler

```csharp
public class UserRegisteredIntegrationEventHandler(ISender sender)
    : IIntegrationEventHandler<UserRegisteredIntegrationEvent>
{
    public async Task HandleAsync(UserRegisteredIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        await sender.Send(new CreateUserProfileCommand(integrationEvent.UserId, integrationEvent.Email), cancellationToken);
    }
}
```

---

## Configuration Reference

### `MessagingOptions` (section: `Messaging`)

| Property | Default | Description |
|----------|---------|-------------|
| `Exchange` | `""` | Default RabbitMQ exchange name for outbox publishing |
| `ProcessingInterval` | `00:00:05` | How often the outbox processor polls for unprocessed messages |
| `BatchSize` | `50` | Max messages processed per polling cycle |
| `MaxRetryAttempts` | `3` | Max publish attempts before a message is marked dead |

```json
"Messaging": {
  "Exchange": "my-app.events",
  "ProcessingInterval": "00:00:05",
  "BatchSize": 50,
  "MaxRetryAttempts": 3
}
```

### `RabbitMqOptions` (section: `RabbitMq`)

| Property | Default | Description |
|----------|---------|-------------|
| `Host` | `localhost` | RabbitMQ hostname |
| `Port` | `5672` | AMQP port |
| `VirtualHost` | `/` | RabbitMQ virtual host |
| `Username` | `""` | RabbitMQ username (required) |
| `Password` | `""` | RabbitMQ password (required) |

```json
"RabbitMq": {
  "Host": "rabbitmq",
  "Port": 5672,
  "VirtualHost": "/",
  "Username": "guest",
  "Password": "guest"
}
```

### `RabbitMqConsumerOptions` (section: `RabbitMqConsumer`)

| Property | Default | Description |
|----------|---------|-------------|
| `Exchange` | `""` | Exchange to bind the consumer queue to |
| `QueueName` | `""` | Queue name to declare and consume from |
| `DeadLetterExchange` | `""` | Dead-letter exchange for failed messages |
| `DeadLetterQueue` | `""` | Dead-letter queue name |
| `PrefetchCount` | `10` | Max unacknowledged messages per consumer |

```json
"RabbitMqConsumer": {
  "Exchange": "my-app.events",
  "QueueName": "my-consumer-service",
  "DeadLetterExchange": "my-app.events.dlx",
  "DeadLetterQueue": "my-consumer-service.dlq",
  "PrefetchCount": 10
}
```

---

## How the Outbox Works

- **Atomicity**: `OutboxSaveChangesInterceptor` runs inside the same `SaveChangesAsync` call, so outbox rows are committed in the same DB transaction as entity changes.
- **Polling**: `OutboxProcessorService<TDbContext>` polls every `ProcessingInterval` and uses `FOR UPDATE SKIP LOCKED` so multiple replicas can run safely without double-processing.
- **Retry**: Failed publishes increment `RetryCount`. Once `MaxRetryAttempts` is exceeded, the message is marked processed-with-error and removed from the active polling set.
- **Routing key derivation**: `UserRegisteredIntegrationEvent` -> `user.registered` (PascalCase parts lower-cased, `IntegrationEvent` suffix stripped, joined with `.`).

---

## `OutboxMessage` Schema

`OutboxMessage` maps to the `outbox_messages` table (added via `modelBuilder.AddOutboxMessages()`).

| Column | Type | Description |
|--------|------|-------------|
| `id` | `uuid` | Version 7 UUID (sortable by creation time) |
| `event_type` | `text` | Assembly-qualified CLR type name |
| `payload` | `text` | JSON-serialized integration event |
| `exchange` | `text` | Target RabbitMQ exchange |
| `routing_key` | `text` | Derived routing key |
| `created_at` | `timestamptz` | UTC insert time |
| `processed_at` | `timestamptz?` | UTC processed time (`null` = unprocessed) |
| `retry_count` | `int` | Number of publish attempts |
| `error` | `text?` | Last error message on failure |

A partial index on `processed_at IS NULL` keeps polling queries fast.

---

## DI Registration Summary

### Publisher side

| Method | What it registers |
|--------|------------------|
| `AddMessaging(configure?)` | `OutboxSaveChangesInterceptor` (singleton), `IntegrationEventCollector` (scoped), `IIntegrationEventPublisher` (scoped), `IDomainEventDispatcher` (scoped), `MessagingOptions` |
| `AddOutboxProcessor<TDbContext>()` | `OutboxProcessorService<TDbContext>` as a hosted service |
| `AddDomainEventHandler<TEvent, THandler>()` | Scoped `IDomainEventHandler<TEvent>` (AOT-safe) |
| `AddDomainEventHandlersFromAssembly(Assembly)` | Scans assembly and registers all `IDomainEventHandler<T>` implementations (reflection-based) |
| `AddDomainEventHandlersFromAssemblyContaining<T>()` | Same behavior, scoped to the assembly containing `T` |

### Transport (RabbitMQ)

| Method | What it registers |
|--------|------------------|
| `AddRabbitMqPublisher(configure?)` | Singleton `IConnection`, scoped `IRabbitMqPublisher` -> `RabbitMqPublisher` |
| `AddRabbitMqConsumer(configure?)` | `IConnectionFactory` (singleton), `RabbitMqConsumerHostedService` (hosted service) |
| `AddIntegrationEventHandler<TEvent, THandler>()` | Scoped `IIntegrationEventHandler<TEvent>` |
