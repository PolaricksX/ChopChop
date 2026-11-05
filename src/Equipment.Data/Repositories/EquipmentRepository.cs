using Equipment.Data.Context;
using Equipment.Domain.Entities;
using Equipment.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace Equipment.Data.Repositories;

public sealed class EquipmentRepository : EfRepository<EquipmentItem>, IEquipmentRepository
{
    private readonly EquipmentDbContext context;

    public EquipmentRepository(EquipmentDbContext context) : base(context) =>
        this.context = context;

    public async Task<EquipmentPage> ListOwnedAsync(
        string ownerId,
        EquipmentQuery query,
        CancellationToken token)
    {
        ValidateOwnerId(ownerId);
        if (query.Page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(query), "Page must be greater than zero.");
        }

        if (query.PageSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(query), "Page size must be between 1 and 100.");
        }

        if (query.Type.HasValue && !Enum.IsDefined(query.Type.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(query), "Equipment type is invalid.");
        }

        if (query.Condition.HasValue && !Enum.IsDefined(query.Condition.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(query), "Equipment condition is invalid.");
        }

        if (query.Availability.HasValue && !Enum.IsDefined(query.Availability.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(query), "Equipment availability is invalid.");
        }

        var skipValue = (long)(query.Page - 1) * query.PageSize;
        if (skipValue > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(query), "Page is outside the supported range.");
        }

        var items = context.EquipmentItems.AsNoTracking()
            .Where(item => item.OwnerId == ownerId);
        if (query.Type.HasValue)
        {
            items = items.Where(item => item.Type == query.Type.Value);
        }

        if (query.Condition.HasValue)
        {
            items = items.Where(item => item.Condition == query.Condition.Value);
        }

        if (query.Availability.HasValue)
        {
            items = items.Where(item => item.Availability == query.Availability.Value);
        }

        var search = NormalizeSearch(query.Search);
        if (search is not null)
        {
            items = items.Where(item =>
                item.NormalizedName.Contains(search) ||
                (item.Brand != null && item.Brand.ToUpper().Contains(search)) ||
                (item.Size != null && item.Size.ToUpper().Contains(search)) ||
                (item.Notes != null && item.Notes.ToUpper().Contains(search)) ||
                (item.Location != null && item.Location.ToUpper().Contains(search)));
        }

        var totalCount = await items.CountAsync(token);
        var pageItems = await items
            .OrderBy(item => item.NormalizedName)
            .ThenBy(item => item.Type)
            .ThenBy(item => item.Id)
            .Skip((int)skipValue)
            .Take(query.PageSize)
            .ToListAsync(token);

        return new EquipmentPage(pageItems, query.Page, query.PageSize, totalCount);
    }

    public Task<EquipmentItem?> GetOwnedAsync(
        Guid id,
        string ownerId,
        bool forUpdate,
        CancellationToken token)
    {
        ValidateOwnerId(ownerId);
        var query = context.EquipmentItems
            .Where(item => item.Id == id && item.OwnerId == ownerId);
        if (!forUpdate)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync(token);
    }

    public Task<bool> DuplicateExistsAsync(
        string ownerId,
        string normalizedName,
        EquipmentType type,
        Guid? excludingId,
        CancellationToken token)
    {
        ValidateOwnerId(ownerId);
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        var query = context.EquipmentItems.AsNoTracking()
            .Where(item =>
                item.OwnerId == ownerId &&
                item.NormalizedName == normalizedName &&
                item.Type == type);
        if (excludingId.HasValue)
        {
            query = query.Where(item => item.Id != excludingId.Value);
        }

        return query.AnyAsync(token);
    }

    public void Delete(EquipmentItem item) => context.EquipmentItems.Remove(item);

    public void SetExpectedVersion(EquipmentItem item, byte[] version) =>
        context.Entry(item).Property(equipment => equipment.RowVersion).OriginalValue = version;

    private static void ValidateOwnerId(string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId) || ownerId.Length > EquipmentItem.MaxOwnerIdLength)
        {
            throw new ArgumentException("A valid owner id is required.", nameof(ownerId));
        }
    }

    private static string? NormalizeSearch(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        var normalized = string.Join(
            ' ',
            search.Normalize(NormalizationForm.FormKC)
                .Trim()
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length > EquipmentQuery.MaxSearchLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(search),
                $"Search must be no more than {EquipmentQuery.MaxSearchLength} characters.");
        }

        return normalized.Length == 0 ? null : normalized.ToUpperInvariant();
    }
}
