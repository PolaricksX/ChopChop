using Equipment.Data.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Equipment.Data.Configurations;

internal sealed class EquipmentIdempotencyRecordConfiguration
    : IEntityTypeConfiguration<EquipmentIdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<EquipmentIdempotencyRecord> builder)
    {
        builder.ToTable("EquipmentIdempotencyRecords");
        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id).ValueGeneratedNever();
        builder.Property(record => record.OwnerId)
            .HasMaxLength(200)
            .UseCollation("Latin1_General_100_BIN2")
            .IsRequired();
        builder.Property(record => record.Key)
            .HasMaxLength(128)
            .UseCollation("Latin1_General_100_BIN2")
            .IsRequired();
        builder.Property(record => record.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(record => record.ResourceId).IsRequired();
        builder.Property(record => record.CreatedAtUtc).IsRequired();
        builder.HasIndex(record => new { record.OwnerId, record.Key })
            .IsUnique()
            .HasDatabaseName("UX_EquipmentIdempotency_Owner_Key");
    }
}
