using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RecipeManagement.Data.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> entity)
    {
        entity.ToTable("OutboxMessages"); entity.HasKey(x => x.Id); entity.Property(x => x.EventType).HasMaxLength(160).IsRequired();
        entity.Property(x => x.RoutingKey).HasMaxLength(200).IsRequired(); entity.Property(x => x.CorrelationId).HasMaxLength(128).IsRequired();
        entity.Property(x => x.Payload).IsRequired(); entity.Property(x => x.LastError).HasMaxLength(500);
        entity.HasIndex(x => new { x.PublishedAtUtc, x.DeadLetteredAtUtc, x.NextAttemptAtUtc });
    }
}

public sealed class ProcessedEventConfiguration : IEntityTypeConfiguration<ProcessedEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedEvent> entity) { entity.ToTable("ProcessedEvents"); entity.HasKey(x => x.EventId); }
}

public sealed class UserDeletionOutcomeConfiguration : IEntityTypeConfiguration<UserDeletionOutcome>
{
    public void Configure(EntityTypeBuilder<UserDeletionOutcome> entity)
    { entity.ToTable("UserDeletionOutcomes"); entity.HasKey(x => x.EventId); entity.HasIndex(x => x.UserId); }
}

public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> entity)
    { entity.ToTable("IdempotencyRecords"); entity.HasKey(x => x.Id); entity.HasIndex(x => new { x.OwnerId, x.Key }).IsUnique(); entity.Property(x => x.Key).HasMaxLength(200).IsRequired(); entity.Property(x => x.RequestFingerprint).HasMaxLength(128).IsRequired(); entity.Property(x => x.ResponsePayload).IsRequired(); }
}
