using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Clywell.Core.Messaging;

/// <summary>
/// Background service that polls the <c>outbox_messages</c> table and publishes
/// unprocessed messages to the message broker via <see cref="IRabbitMqPublisher"/>.
/// Uses <c>FOR UPDATE SKIP LOCKED</c> (via EF Core row-level locking) to safely
/// run multiple replicas without duplicate delivery.
/// </summary>
/// <typeparam name="TDbContext">
/// The EF Core <see cref="DbContext"/> that owns the <c>outbox_messages</c> table.
/// </typeparam>
public sealed class OutboxProcessorService<TDbContext>(
    IServiceScopeFactory scopeFactory,
    IOptions<MessagingOptions> options,
    ILogger<OutboxProcessorService<TDbContext>> logger)
    : BackgroundService
    where TDbContext : DbContext
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox processor encountered an error. Retrying after interval.");
            }

            await Task.Delay(options.Value.ProcessingInterval, stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IRabbitMqPublisher>();

        // Use raw SQL for FOR UPDATE SKIP LOCKED to ensure safe concurrent processing
        var messages = await dbContext.Set<OutboxMessage>()
            .FromSqlRaw(
                """
                SELECT * FROM outbox_messages
                WHERE processed_at IS NULL
                ORDER BY created_at
                LIMIT {0}
                FOR UPDATE SKIP LOCKED
                """,
                options.Value.BatchSize)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
            return;

        foreach (var message in messages)
        {
            try
            {
                await publisher.PublishAsync(message, cancellationToken);
                message.ProcessedAt = DateTimeOffset.UtcNow;
                message.Error = null;
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.Error = ex.Message;

                if (message.RetryCount >= options.Value.MaxRetryAttempts)
                {
                    logger.LogError(
                        ex,
                        "Outbox message {MessageId} ({EventType}) exceeded max retry attempts and will not be retried.",
                        message.Id,
                        message.EventType);

                    // Mark as processed with error to prevent infinite retry - dead-letter manually if needed
                    message.ProcessedAt = DateTimeOffset.UtcNow;
                }
                else
                {
                    logger.LogWarning(
                        ex,
                        "Failed to publish outbox message {MessageId} ({EventType}). Retry {RetryCount}/{MaxRetries}.",
                        message.Id,
                        message.EventType,
                        message.RetryCount,
                        options.Value.MaxRetryAttempts);
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
