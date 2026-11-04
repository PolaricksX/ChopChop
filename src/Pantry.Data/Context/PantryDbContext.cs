using System.Text.Json;
using ChopChop.Shared;
using Microsoft.EntityFrameworkCore;
using Pantry.Data.Persistence;
using Pantry.Domain.Entities;
using Pantry.Domain.Enums;

namespace Pantry.Data.Context;

public sealed class PantryDbContext(
    DbContextOptions<PantryDbContext> options,
    ICorrelationIdAccessor? correlation = null) : DbContext(options)
{
    public DbSet<PantryItem> PantryItems => Set<PantryItem>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PantryDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        AddMutationEvents();
        foreach (var entry in ChangeTracker.Entries<PantryItem>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Property<string>("OwnerIdNormalized").CurrentValue = NormalizeKey(entry.Entity.OwnerId);
                entry.Property<string>("NameNormalized").CurrentValue = NormalizeKey(entry.Entity.Name);
            }
        }

        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void AddMutationEvents()
    {
        var occurredAt = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<PantryItem>()
                     .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                     .ToList())
        {
            var type = entry.State switch
            {
                EntityState.Added => "PantryItemAdded",
                EntityState.Deleted => "PantryItemDeleted",
                _ => MutationType(entry)
            };
            var item = entry.Entity;
            var payload = JsonSerializer.Serialize(new EventEnvelope<object>(
                Guid.NewGuid(), $"pantry.item.{type["PantryItem".Length..].ToLowerInvariant()}",
                1, occurredAt, "pantry", correlation?.Current ?? "system", new
                {
                    item.Id,
                    item.OwnerId,
                    item.Name,
                    item.Type,
                    item.CustomCategory,
                    item.Notes,
                    item.Quantity,
                    item.Unit,
                    item.ExpiresAt,
                    item.Availability,
                    item.Version
                }));
            OutboxMessages.Add(new OutboxMessage(Guid.NewGuid(), type, payload, occurredAt,
                correlation?.Current ?? "system"));
        }
    }

    private static string MutationType(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<PantryItem> entry)
    {
        var previous = entry.OriginalValues.GetValue<Availability>(nameof(PantryItem.Availability));
        return previous == entry.Entity.Availability
            ? "PantryItemUpdated"
            : entry.Entity.Availability switch
            {
                Availability.Consumed => "PantryItemConsumed",
                Availability.Unavailable => "PantryItemUnavailable",
                Availability.Available => "PantryItemAvailable",
                _ => "PantryItemUpdated"
            };
    }

    internal static string NormalizeKey(string value) =>
        string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant();
}
