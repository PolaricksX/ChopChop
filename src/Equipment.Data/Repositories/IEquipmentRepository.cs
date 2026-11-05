using Equipment.Domain.Entities;
using Equipment.Domain.Enums;

namespace Equipment.Data.Repositories;

public interface IEquipmentRepository : IRepository<EquipmentItem>
{
    Task<EquipmentPage> ListOwnedAsync(
        string ownerId,
        EquipmentQuery query,
        CancellationToken token);

    Task<EquipmentItem?> GetOwnedAsync(
        Guid id,
        string ownerId,
        bool forUpdate,
        CancellationToken token);

    Task<bool> DuplicateExistsAsync(
        string ownerId,
        string normalizedName,
        EquipmentType type,
        Guid? excludingId,
        CancellationToken token);

    void Delete(EquipmentItem item);

    void SetExpectedVersion(EquipmentItem item, byte[] version);
}

public sealed record EquipmentQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    EquipmentType? Type = null,
    EquipmentCondition? Condition = null,
    EquipmentAvailability? Availability = null)
{
    public const int MaxSearchLength = 120;
}

public sealed record EquipmentPage(
    IReadOnlyList<EquipmentItem> Items,
    int Page,
    int PageSize,
    int TotalCount);
