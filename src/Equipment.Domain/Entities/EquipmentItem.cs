using System.Text;
using Equipment.Domain.Enums;
using Equipment.Domain.Exceptions;

namespace Equipment.Domain.Entities;

public sealed class EquipmentItem
{
    public const int MaxOwnerIdLength = 200;
    public const int MaxNameLength = 120;
    public const int MaxBrandLength = 100;
    public const int MaxSizeLength = 100;
    public const int MaxNotesLength = 500;
    public const int MaxLocationLength = 120;
    public const int MaxQuantity = 1_000;

    private EquipmentItem()
    {
    }

    public EquipmentItem(
        Guid id,
        string ownerId,
        string name,
        EquipmentType type,
        int quantity,
        DateTimeOffset now,
        EquipmentCondition condition = EquipmentCondition.Good,
        string? brand = null,
        string? size = null,
        string? notes = null,
        string? location = null)
    {
        if (id == Guid.Empty)
        {
            throw new EquipmentDomainException("An equipment id is required.", nameof(id));
        }

        Id = id;
        OwnerId = ValidateOwnerId(ownerId);
        Type = ValidateType(type);
        Quantity = ValidateQuantity(quantity);
        Condition = ValidateCondition(condition);
        SetName(name);
        Brand = NormalizeOptional(brand, MaxBrandLength, nameof(brand));
        Size = NormalizeOptional(size, MaxSizeLength, nameof(size));
        Notes = NormalizeOptional(notes, MaxNotesLength, nameof(notes));
        Location = NormalizeOptional(location, MaxLocationLength, nameof(location));
        Availability = EquipmentAvailability.Available;
        CreatedAtUtc = now.ToUniversalTime();
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }

    public string OwnerId { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public EquipmentType Type { get; private set; }

    public int Quantity { get; private set; }

    public string? Brand { get; private set; }

    public string? Size { get; private set; }

    public string? Notes { get; private set; }

    public EquipmentCondition Condition { get; private set; }

    public string? Location { get; private set; }

    public EquipmentAvailability Availability { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public bool HasSameIdentity(EquipmentItem other) =>
        other is not null &&
        string.Equals(OwnerId, other.OwnerId, StringComparison.Ordinal) &&
        string.Equals(NormalizedName, other.NormalizedName, StringComparison.Ordinal) &&
        Type == other.Type;

    public bool Update(
        string name,
        EquipmentType type,
        int quantity,
        EquipmentCondition condition,
        string? brand,
        string? size,
        string? notes,
        string? location,
        DateTimeOffset now)
    {
        var normalizedName = NormalizeRequired(name, MaxNameLength, nameof(name));
        var validatedType = ValidateType(type);
        var validatedQuantity = ValidateQuantity(quantity);
        var validatedCondition = ValidateCondition(condition);
        var normalizedBrand = NormalizeOptional(brand, MaxBrandLength, nameof(brand));
        var normalizedSize = NormalizeOptional(size, MaxSizeLength, nameof(size));
        var normalizedNotes = NormalizeOptional(notes, MaxNotesLength, nameof(notes));
        var normalizedLocation = NormalizeOptional(location, MaxLocationLength, nameof(location));

        if (Name == normalizedName &&
            Type == validatedType &&
            Quantity == validatedQuantity &&
            Condition == validatedCondition &&
            Brand == normalizedBrand &&
            Size == normalizedSize &&
            Notes == normalizedNotes &&
            Location == normalizedLocation)
        {
            return false;
        }

        Name = normalizedName;
        NormalizedName = NormalizeKey(normalizedName);
        Type = validatedType;
        Quantity = validatedQuantity;
        Condition = validatedCondition;
        Brand = normalizedBrand;
        Size = normalizedSize;
        Notes = normalizedNotes;
        Location = normalizedLocation;
        Touch(now);
        return true;
    }

    public void SetQuantity(int quantity, DateTimeOffset now)
    {
        Quantity = ValidateQuantity(quantity);
        Touch(now);
    }

    public void SetCondition(EquipmentCondition condition, DateTimeOffset now)
    {
        var validatedCondition = ValidateCondition(condition);
        if (Condition == validatedCondition)
        {
            return;
        }

        Condition = validatedCondition;
        Touch(now);
    }

    public void SetAvailability(EquipmentAvailability availability, DateTimeOffset now)
    {
        if (!Enum.IsDefined(availability))
        {
            throw new EquipmentDomainException("A valid availability is required.", nameof(availability));
        }

        if (Availability == availability)
        {
            return;
        }

        Availability = availability;
        Touch(now);
    }

    private void SetName(string name)
    {
        Name = NormalizeRequired(name, MaxNameLength, nameof(name));
        NormalizedName = NormalizeKey(Name);
    }

    private void Touch(DateTimeOffset now) => UpdatedAtUtc = now.ToUniversalTime();

    private static EquipmentType ValidateType(EquipmentType type)
    {
        if (!Enum.IsDefined(type))
        {
            throw new EquipmentDomainException("A valid equipment type is required.", nameof(type));
        }

        return type;
    }

    private static EquipmentCondition ValidateCondition(EquipmentCondition condition)
    {
        if (!Enum.IsDefined(condition))
        {
            throw new EquipmentDomainException("A valid equipment condition is required.", nameof(condition));
        }

        return condition;
    }

    private static int ValidateQuantity(int quantity)
    {
        if (quantity is < 1 or > MaxQuantity)
        {
            throw new EquipmentDomainException(
                $"Quantity must be between 1 and {MaxQuantity}.",
                nameof(quantity));
        }

        return quantity;
    }

    private static string ValidateOwnerId(string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId) || ownerId.Length > MaxOwnerIdLength)
        {
            throw new EquipmentDomainException(
                $"An owner id of 1 to {MaxOwnerIdLength} characters is required.",
                nameof(ownerId));
        }

        return ownerId;
    }

    private static string NormalizeRequired(string value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new EquipmentDomainException("A value is required.", parameterName);
        }

        var normalized = NormalizeWhitespace(value);
        if (normalized.Length > maxLength)
        {
            throw new EquipmentDomainException($"Value must be no more than {maxLength} characters.", parameterName);
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : NormalizeRequired(value, maxLength, parameterName);

    private static string NormalizeWhitespace(string value) =>
        string.Join(
            ' ',
            value.Normalize(NormalizationForm.FormKC)
                .Trim()
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string NormalizeKey(string value) => value.ToUpperInvariant();
}
