using Equipment.Domain.Entities;
using Equipment.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Equipment.Data.Context;

public sealed class EquipmentDbContext(DbContextOptions<EquipmentDbContext> options) : DbContext(options)
{
    public DbSet<EquipmentItem> EquipmentItems => Set<EquipmentItem>();

    public DbSet<EquipmentIdempotencyRecord> IdempotencyRecords => Set<EquipmentIdempotencyRecord>();

    public DbSet<EquipmentOutboxMessage> OutboxMessages => Set<EquipmentOutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("equipment");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EquipmentDbContext).Assembly);
    }
}
