using MealPlanner.Data.Events;
using MealPlanner.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MealPlanner.Data.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("MealPlanOutbox");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.EventType).HasMaxLength(128).IsRequired();
        builder.Property(message => message.SourceService).HasMaxLength(128).IsRequired();
        builder.Property(message => message.CorrelationId).HasMaxLength(128).IsRequired();
        builder.Property(message => message.Payload).IsRequired();
        builder.Property(message => message.LastError).HasMaxLength(64);
        builder.HasIndex(message => new { message.PublishedAt, message.DeadLetteredAt, message.NextAttemptAt });
    }

    internal sealed class MealPlanIdempotencyRecordConfiguration : IEntityTypeConfiguration<MealPlanIdempotencyRecord>
    {
        public void Configure(EntityTypeBuilder<MealPlanIdempotencyRecord> builder)
        {
            builder.ToTable("MealPlanIdempotencyRecords");
            builder.HasKey(record => record.Id);
            builder.Property(record => record.OwnerId).HasMaxLength(256).IsRequired();
            builder.Property(record => record.Key).HasMaxLength(128).IsRequired();
            builder.Property(record => record.RequestHash).HasMaxLength(64).IsRequired();
            builder.Property(record => record.ResourceType).HasMaxLength(16).IsRequired();
            builder.HasIndex(record => new { record.OwnerId, record.Key })
                .IsUnique()
                .HasDatabaseName("UX_MealPlanIdempotency_Owner_Key");
        }
    }
}
