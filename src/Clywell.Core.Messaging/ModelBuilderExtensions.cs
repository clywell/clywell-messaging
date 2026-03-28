using Microsoft.EntityFrameworkCore;

namespace Clywell.Core.Messaging;

/// <summary>
/// Extension methods for <see cref="ModelBuilder"/> to configure messaging outbox entities.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Applies <see cref="OutboxMessageConfiguration"/> to the model builder,
    /// configuring the <c>outbox_messages</c> table.
    /// </summary>
    public static ModelBuilder AddOutboxMessages(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        return modelBuilder;
    }
}
