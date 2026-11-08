using Equipment.Domain.Enums;

namespace Equipment.Api.Contracts;

public sealed record EquipmentUpsertRequest(
    string? Name,
    EquipmentType? Type,
    int? Quantity,
    EquipmentCondition? Condition,
    string? Brand,
    string? Size,
    string? Notes,
    string? Location);

public sealed record EquipmentAvailabilityRequest(bool? IsAvailable);

public sealed record EquipmentListRequest(
    int Page,
    int PageSize,
    string? Search,
    EquipmentType? Type,
    EquipmentCondition? Condition,
    EquipmentAvailability? Availability);

public sealed record EquipmentResponse(
    Guid Id,
    string Name,
    EquipmentType Type,
    int Quantity,
    string? Brand,
    string? Size,
    string? Notes,
    EquipmentCondition Condition,
    string? Location,
    EquipmentAvailability Availability,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string Version);

public sealed record EquipmentPageResponse(
    IReadOnlyList<EquipmentResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
