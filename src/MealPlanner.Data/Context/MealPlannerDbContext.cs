using MealPlanner.Data.Events;
using MealPlanner.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Data.Context;

public sealed class MealPlannerDbContext(DbContextOptions<MealPlannerDbContext> options) : DbContext(options)
{
    public DbSet<MealPlan> MealPlans => Set<MealPlan>();

    public DbSet<MealEntry> MealEntries => Set<MealEntry>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<MealPlanIdempotencyRecord> IdempotencyRecords => Set<MealPlanIdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MealPlannerDbContext).Assembly);
}
