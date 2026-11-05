using Equipment.Data.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Equipment.Data.Configurations;

internal sealed class EquipmentOutboxMessageConfiguration : IEntityTypeConfiguration<EquipmentOutboxMessage>
{
    public void Configure(EntityTypeBuilder<EquipmentOutboxMessage> builder)
    {
        builder.ToTable("EquipmentOutbox");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).ValueGeneratedNever();
        builder.Property(message => message.EventType).HasMaxLength(128).IsRequired();
        builder.Property(message => message.RoutingKey).HasMaxLength(128).IsRequired();
        builder.Property(message => message.Payload).IsRequired();
        builder.Property(message => message.CorrelationId).HasMaxLength(64).IsRequired();
        builder.Property(message => message.OccurredAtUtc).IsRequired();
        builder.Property(message => message.LastError).HasMaxLength(64);
        builder.HasIndex(message => new
        {
            message.PublishedAtUtc,
            message.DeadLetteredAtUtc,
            message.NextAttemptAtUtc,
        }).HasDatabaseName("IX_EquipmentOutbox_Dispatch");
    }
}
