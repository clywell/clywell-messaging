using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clywell.Core.Messaging;

/// <summary>
/// EF Core entity configuration for <see cref="OutboxMessage"/>.
/// Apply via <see cref="ModelBuilderExtensions.AddOutboxMessages"/>.
/// </summary>
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .ValueGeneratedNever();

        builder.Property(m => m.EventType)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(m => m.Payload)
            .IsRequired();

        builder.Property(m => m.Exchange)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(m => m.RoutingKey)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(m => m.Error)
            .HasMaxLength(2000);

        builder.HasIndex(m => m.ProcessedAt)
            .HasFilter("processed_at IS NULL");
    }
}
