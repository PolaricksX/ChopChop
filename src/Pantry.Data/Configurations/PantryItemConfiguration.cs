using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pantry.Domain.Entities;

namespace Pantry.Data.Configurations;

public sealed class PantryItemConfiguration : IEntityTypeConfiguration<PantryItem>
{
    public void Configure(EntityTypeBuilder<PantryItem> builder)
    {
        builder.ToTable("PantryItems");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.OwnerId).HasMaxLength(200).IsRequired();
        builder.Property(item => item.Name).HasMaxLength(120).IsRequired();
        builder.Property(item => item.CustomCategory).HasMaxLength(60);
        builder.Property(item => item.Notes).HasMaxLength(500);
        builder.Property(item => item.Quantity).HasPrecision(18, 3).IsRequired();
        builder.Property(item => item.Type).IsRequired();
        builder.Property(item => item.Unit).IsRequired();
        builder.Property(item => item.Availability).IsRequired();
        builder.Property(item => item.CreatedAt).IsRequired();
        builder.Property(item => item.UpdatedAt).IsRequired();
        builder.Property(item => item.Version).IsRowVersion().IsRequired();

        builder.Property<string>("OwnerKey")
            .HasMaxLength(64).IsUnicode(false).IsRequired();
        builder.Property<string>("NameNormalized")
            .HasMaxLength(120).IsRequired();

        builder.HasIndex(
                "OwnerKey",
                "NameNormalized",
                nameof(PantryItem.Type))
            .IsUnique()
            .HasDatabaseName("UX_PantryItems_ActiveOwnerIngredientType")
            .HasFilter("[Availability] = 0");
    }
}
