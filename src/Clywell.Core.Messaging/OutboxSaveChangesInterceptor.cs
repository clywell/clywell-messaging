using System.Text.Json;
using Clywell.Core.Data;
using Clywell.Core.Data.EntityFramework;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Clywell.Core.Messaging;

/// <summary>
/// EF Core interceptor that implements the transactional outbox pattern.
/// Intercepts <c>SavingChangesAsync</c> (pre-commit), dispatches domain events to
/// registered <see cref="IDomainEventHandler{T}"/> implementations, collects any
/// integration events published via <see cref="IIntegrationEventPublisher"/>, and
/// writes them as <see cref="OutboxMessage"/> rows within the same database transaction.
/// </summary>
/// <remarks>
/// Register as a singleton. Wire into a <c>DbContext</c> using
/// <see cref="DbContextOptionsBuilderExtensions.UseOutboxInterceptor"/>.
/// </remarks>
public sealed class OutboxSaveChangesInterceptor(
    IServiceScopeFactory scopeFactory,
    IOptions<MessagingOptions> options)
    : SaveChangesInterceptor
{
    /// <inheritdoc />
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var dbContext = eventData.Context;
        if (dbContext is null)
            return await base.SavingChangesAsync(eventData, result, cancellationToken);

        var entitiesWithEvents = dbContext.ChangeTracker
            .Entries<IHasDomainEvents>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        if (entitiesWithEvents.Count == 0)
            return await base.SavingChangesAsync(eventData, result, cancellationToken);

        using var scope = scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
        var collector = scope.ServiceProvider.GetRequiredService<IntegrationEventCollector>();

        foreach (var entity in entitiesWithEvents)
        {
            await dispatcher.DispatchAsync(entity.DomainEvents, cancellationToken);
            entity.ClearDomainEvents();
        }

        var timestamp = DateTimeOffset.UtcNow;

        foreach (var integrationEvent in collector.Collected)
        {
            var eventType = integrationEvent.GetType();

            var outboxMessage = new OutboxMessage
            {
                Id = Guid.CreateVersion7(),
                EventType = eventType.AssemblyQualifiedName!,
                Payload = JsonSerializer.Serialize(integrationEvent, eventType),
                Exchange = options.Value.Exchange,
                RoutingKey = DeriveRoutingKey(eventType.Name),
                CreatedAt = timestamp,
            };

            await dbContext.Set<OutboxMessage>().AddAsync(outboxMessage, cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Derives a dot-separated lowercase routing key from an integration event type name.
    /// Example: <c>UserRegisteredIntegrationEvent</c> -&gt; <c>user.registered</c>
    /// </summary>
    private static string DeriveRoutingKey(string eventTypeName)
    {
        const string suffix = "IntegrationEvent";

        var name = eventTypeName.EndsWith(suffix, StringComparison.Ordinal)
            ? eventTypeName[..^suffix.Length]
            : eventTypeName;

        return string.Concat(
            name.Select((c, i) => i > 0 && char.IsUpper(c)
                ? $".{char.ToLowerInvariant(c)}"
                : char.ToLowerInvariant(c).ToString()));
    }
}
