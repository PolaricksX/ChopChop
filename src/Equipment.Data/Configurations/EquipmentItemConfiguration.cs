using Equipment.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Equipment.Data.Configurations;

internal sealed class EquipmentItemConfiguration : IEntityTypeConfiguration<EquipmentItem>
{
    public void Configure(EntityTypeBuilder<EquipmentItem> builder)
    {
        builder.ToTable(
            "EquipmentItems",
            table => table.HasCheckConstraint(
                "CK_EquipmentItems_Quantity",
                $"[Quantity] >= 1 AND [Quantity] <= {EquipmentItem.MaxQuantity}"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.OwnerId)
            .HasMaxLength(EquipmentItem.MaxOwnerIdLength)
            .UseCollation("Latin1_General_100_BIN2")
            .IsRequired();
        builder.Property(item => item.Name).HasMaxLength(EquipmentItem.MaxNameLength).IsRequired();
        builder.Property(item => item.NormalizedName)
            .HasMaxLength(EquipmentItem.MaxNameLength)
            .UseCollation("Latin1_General_100_BIN2")
            .IsRequired();
        builder.Property(item => item.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(item => item.Quantity).IsRequired();
        builder.Property(item => item.Brand).HasMaxLength(EquipmentItem.MaxBrandLength);
        builder.Property(item => item.Size).HasMaxLength(EquipmentItem.MaxSizeLength);
        builder.Property(item => item.Notes).HasMaxLength(EquipmentItem.MaxNotesLength);
        builder.Property(item => item.Condition).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(item => item.Location).HasMaxLength(EquipmentItem.MaxLocationLength);
        builder.Property(item => item.Availability).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(item => item.CreatedAtUtc).IsRequired();
        builder.Property(item => item.UpdatedAtUtc).IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion();

        builder.HasIndex(item => new { item.OwnerId, item.NormalizedName, item.Type })
            .IsUnique()
            .HasDatabaseName("UX_Equipment_Owner_NormalizedName_Type");
        builder.HasIndex(item => new
        {
            item.OwnerId,
            item.Type,
            item.Condition,
            item.Availability,
            item.NormalizedName,
        }).HasDatabaseName("IX_Equipment_Owner_Filters_Name");
    }
}
