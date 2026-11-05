using Microsoft.EntityFrameworkCore;
using Pantry.Data.Context;
using Pantry.Domain.Entities;
using Pantry.Domain.Enums;

namespace Pantry.Data.Repositories;

public sealed class PantryRepository(PantryDbContext db)
{
    public async Task<List<PantryItem>> ListAsync(
        string ownerId,
        string? search,
        int skip,
        int take,
        CancellationToken token)
    {
        var query = db.PantryItems.AsNoTracking().Where(item => item.OwnerId == ownerId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpper();
            query = query.Where(item => item.Name.ToUpper().Contains(normalizedSearch));
        }

        return await query
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Id)
            .Skip(Math.Max(skip, 0))
            .Take(Math.Max(take, 0))
            .ToListAsync(token);
    }

    public async Task<List<PantryItem>> ListFilteredAsync(
        string ownerId,
        string? search,
        PantryType? type,
        Availability? availability,
        DateTimeOffset? expiresBefore,
        int page,
        int pageSize,
        CancellationToken token)
    {
        var query = db.PantryItems.AsNoTracking().Where(item => item.OwnerId == ownerId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpper();
            query = query.Where(item => item.Name.ToUpper().Contains(normalizedSearch));
        }
        if (type is not null)
            query = query.Where(item => item.Type == type);
        if (availability is not null)
            query = query.Where(item => item.Availability == availability);
        if (expiresBefore is not null)
            query = query.Where(item => item.ExpiresAt != null && item.ExpiresAt <= expiresBefore);

        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Max(pageSize, 1);
        return await query
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Id)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(token);
    }

    public Task<PantryItem?> GetAsync(string ownerId, Guid id, CancellationToken token) =>
        db.PantryItems.SingleOrDefaultAsync(item => item.OwnerId == ownerId && item.Id == id, token);

    public Task<bool> ExistsAsync(string ownerId, Guid id, CancellationToken token) =>
        db.PantryItems.AnyAsync(item => item.OwnerId == ownerId && item.Id == id, token);

    public async Task<PantryItem> AddAsync(PantryItem item, CancellationToken token)
    {
        var ownerKey = PantryDbContext.NormalizeKey(item.OwnerId);
        var nameKey = PantryDbContext.NormalizeKey(item.Name);
        if (await HasActiveDuplicateAsync(ownerKey, nameKey, item.Type, token))
            throw new DuplicatePantryItemException();

        db.PantryItems.Add(item);
        try
        {
            await db.SaveChangesAsync(token);
            return item;
        }
        catch (DbUpdateException exception) when (IsDuplicateConstraint(exception))
        {
            throw new DuplicatePantryItemException(exception);
        }
    }

    public async Task<bool> UpdateAsync(PantryItem item, byte[] version, CancellationToken token)
    {
        if (await HasActiveDuplicateAsync(
                PantryDbContext.NormalizeKey(item.OwnerId),
                PantryDbContext.NormalizeKey(item.Name),
                item.Type, token, item.Id))
            throw new DuplicatePantryItemException();
        db.Entry(item).Property(entity => entity.Version).OriginalValue = version;
        try
        {
            await db.SaveChangesAsync(token);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
        catch (DbUpdateException exception) when (IsDuplicateConstraint(exception))
        {
            throw new DuplicatePantryItemException(exception);
        }
    }

    public Task<bool> UpdateAsync(
        string ownerId,
        Guid id,
        string name,
        PantryType type,
        string? customCategory,
        string? notes,
        decimal quantity,
        Unit unit,
        DateTimeOffset? expiresAt,
        byte[] version,
        CancellationToken token) =>
        UpdateOwnedAsync(ownerId, id, name, type, customCategory, notes, quantity, unit, expiresAt, version, token);

    public Task<bool> ConsumeAsync(string ownerId, Guid id, byte[]? version, CancellationToken token) =>
        ChangeAvailabilityAsync(ownerId, id, version, consume: true, token);

    public Task<bool> MarkUnavailableAsync(string ownerId, Guid id, byte[]? version, CancellationToken token) =>
        ChangeAvailabilityAsync(ownerId, id, version, consume: false, token);

    public async Task<bool> DeleteAsync(string ownerId, Guid id, CancellationToken token)
    {
        var item = await GetAsync(ownerId, id, token);
        if (item is null)
            return false;

        db.PantryItems.Remove(item);
        await db.SaveChangesAsync(token);
        return true;
    }

    public async Task<bool> DeleteAsync(PantryItem item, CancellationToken token)
    {
        db.PantryItems.Remove(item);
        await db.SaveChangesAsync(token);
        return true;
    }

    private async Task<bool> UpdateOwnedAsync(
        string ownerId,
        Guid id,
        string name,
        PantryType type,
        string? customCategory,
        string? notes,
        decimal quantity,
        Unit unit,
        DateTimeOffset? expiresAt,
        byte[] version,
        CancellationToken token)
    {
        var item = await GetAsync(ownerId, id, token);
        if (item is null)
            return false;
        var ownerKey = PantryDbContext.NormalizeKey(ownerId);
        var nameKey = PantryDbContext.NormalizeKey(name);
        if (await HasActiveDuplicateAsync(ownerKey, nameKey, type, token, id))
            throw new DuplicatePantryItemException();
        item.Update(name, type, customCategory, notes, quantity, unit, expiresAt);
        return await UpdateAsync(item, version, token);
    }

    private async Task<bool> ChangeAvailabilityAsync(
        string ownerId,
        Guid id,
        byte[]? version,
        bool consume,
        CancellationToken token)
    {
        var item = await GetAsync(ownerId, id, token);
        if (item is null)
            return false;
        if (consume) item.Consume();
        else item.MarkUnavailable();
        if (version is not null)
            db.Entry(item).Property(entity => entity.Version).OriginalValue = version;

        try
        {
            await db.SaveChangesAsync(token);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    private Task<bool> HasActiveDuplicateAsync(
        string ownerKey,
        string nameKey,
        PantryType type,
        CancellationToken token,
        Guid? excludingId = null) =>
        db.PantryItems.AnyAsync(
            item => EF.Property<string>(item, "OwnerIdNormalized") == ownerKey &&
                    EF.Property<string>(item, "NameNormalized") == nameKey &&
                    item.Type == type &&
                    item.Availability == Availability.Available &&
                    (excludingId == null || item.Id != excludingId),
            token);

    private static bool IsDuplicateConstraint(DbUpdateException exception) =>
        exception.InnerException?.Message.Contains("UX_PantryItems_ActiveOwnerIngredientType", StringComparison.OrdinalIgnoreCase) == true;
}

public sealed class DuplicatePantryItemException : InvalidOperationException
{
    public DuplicatePantryItemException() : base("An active pantry item with the same owner, ingredient, and type already exists.") { }
    public DuplicatePantryItemException(Exception innerException)
        : base("An active pantry item with the same owner, ingredient, and type already exists.", innerException) { }
}
