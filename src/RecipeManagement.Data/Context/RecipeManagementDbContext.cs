using Microsoft.EntityFrameworkCore;
using RecipeManagement.Data.Configurations;
using RecipeManagement.Domain.Entities;

namespace RecipeManagement.Data.Context;

public sealed class RecipeManagementDbContext(DbContextOptions<RecipeManagementDbContext> options) : DbContext(options)
{
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeVersion> RecipeVersions => Set<RecipeVersion>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();
    public DbSet<UserDeletionOutcome> UserDeletionOutcomes => Set<UserDeletionOutcome>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("recipe_management");
        modelBuilder.ApplyConfiguration(new RecipeConfiguration());
        modelBuilder.ApplyConfiguration(new RecipeVersionConfiguration());
        modelBuilder.ApplyConfiguration(new IngredientConfiguration());
        modelBuilder.ApplyConfiguration(new InstructionStepConfiguration());
        modelBuilder.ApplyConfiguration(new EquipmentRequirementConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new ProcessedEventConfiguration());
        modelBuilder.ApplyConfiguration(new UserDeletionOutcomeConfiguration());
        modelBuilder.ApplyConfiguration(new IdempotencyRecordConfiguration());
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        SaveChangesAsync(true, cancellationToken);

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<RecipeVersion>().Where(x => x.State == EntityState.Modified))
        {
            var property = entry.Property<int>("ConcurrencyToken");
            property.CurrentValue++;
        }
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
