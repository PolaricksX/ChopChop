using MealPlanner.Domain.Entities;
using MealPlanner.Domain.Enums;
using MealPlanner.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MealPlanner.Data.Configurations;

internal sealed class MealPlanConfiguration : IEntityTypeConfiguration<MealPlan>
{
    public void Configure(EntityTypeBuilder<MealPlan> builder)
    {
        builder.ToTable("MealPlans");
        builder.HasKey(plan => plan.Id);
        builder.Property(plan => plan.Id).ValueGeneratedNever();
        builder.Property(plan => plan.OwnerId).HasMaxLength(MealPlanRules.OwnerMaxLength).IsRequired();
        builder.Property(plan => plan.Name).HasMaxLength(MealPlanRules.NameMaxLength).IsRequired();
        builder.Property(plan => plan.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(plan => plan.RowVersion).IsRowVersion();

        builder.HasIndex(plan => new { plan.OwnerId, plan.CreatedAt });
    }
}

internal sealed class MealEntryConfiguration : IEntityTypeConfiguration<MealEntry>
{
    public void Configure(EntityTypeBuilder<MealEntry> builder)
    {
        builder.ToTable("MealEntries");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).ValueGeneratedNever();
        builder.Property(entry => entry.Title).HasMaxLength(MealPlanRules.TitleMaxLength).IsRequired();
        builder.Property(entry => entry.Slot).HasConversion<int>().IsRequired();
        builder.Property(entry => entry.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.HasOne<MealPlan>()
            .WithMany()
            .HasForeignKey(entry => entry.MealPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(entry => new { entry.MealPlanId, entry.Date, entry.Slot })
            .IsUnique()
            .HasDatabaseName("UX_MealEntries_Plan_Date_Slot")
            .HasFilter($"[Slot] <> {(int)MealSlot.Snack}");
    }
}
