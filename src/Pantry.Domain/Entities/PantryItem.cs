using Pantry.Domain.Enums;

namespace Pantry.Domain.Entities;

public sealed class PantryItem
{
    private const int MaxOwnerIdLength = 200;
    private const int MaxNameLength = 120;
    private const int MaxNotesLength = 500;
    private const int MaxCustomCategoryLength = 60;
    private const decimal MaxQuantity = 1_000_000;

    private PantryItem() { }

    public PantryItem(
        Guid id,
        string ownerId,
        string name,
        PantryType type,
        decimal quantity,
        Unit unit,
        string? notes = null,
        DateTimeOffset? expiresAt = null,
        string? customCategory = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("An item id is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(ownerId) || ownerId.Length > MaxOwnerIdLength || ownerId.Any(char.IsControl))
            throw new ArgumentException("A valid owner id is required.", nameof(ownerId));
        OwnerId = ownerId;
        Id = id;
        Type = ValidateType(type);
        SetCustomCategory(customCategory);
        Rename(name);
        SetQuantity(quantity);
        SetUnit(unit);
        SetNotes(notes);
        SetExpiration(expiresAt);
    }

    public PantryItem(
        Guid id,
        string ownerId,
        string name,
        string customCategory,
        decimal quantity,
        Unit unit,
        string? notes = null,
        DateTimeOffset? expiresAt = null)
        : this(id, ownerId, name, PantryType.Custom, quantity, unit, notes, expiresAt, customCategory)
    {
        SetCustomCategory(customCategory);
    }

    public Guid Id { get; private set; }
    public string OwnerId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public PantryType Type { get; private set; }
    public string? CustomCategory { get; private set; }
    public string? Notes { get; private set; }
    public decimal Quantity { get; private set; }
    public Unit Unit { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public Availability Availability { get; private set; } = Availability.Available;
    public Availability State => Availability;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public byte[] Version { get; private set; } = Guid.NewGuid().ToByteArray();
    public void Rename(string name) { Name = NormalizeRequired(name, MaxNameLength, nameof(name)); Touch(); }
    public void SetQuantity(decimal quantity)
    {
        if (Availability == Availability.Consumed && quantity != 0)
            throw new InvalidOperationException("A consumed item must have zero quantity.");
        if ((Availability == Availability.Available && quantity <= 0) || quantity > MaxQuantity)
            throw new ArgumentOutOfRangeException(nameof(quantity));
        Quantity = quantity;
        Touch();
    }

    public void SetUnit(Unit unit) { Unit = ValidateUnit(unit); Touch(); }
    public void SetCustomCategory(string? customCategory)
    {
        CustomCategory = Type == PantryType.Custom
            ? NormalizeRequired(customCategory ?? string.Empty, MaxCustomCategoryLength, nameof(customCategory))
            : null;
        Touch();
    }
    public void SetNotes(string? notes)
    {
        if (notes is not null && notes.Trim().Length > MaxNotesLength)
            throw new ArgumentException($"Notes must be no more than {MaxNotesLength} characters.", nameof(notes));
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        Touch();
    }

    public void SetExpiration(DateTimeOffset? expiresAt) { ExpiresAt = expiresAt?.ToUniversalTime(); Touch(); }
    public bool IsExpired(DateTimeOffset now) => ExpiresAt is not null && ExpiresAt <= now;
    public void Consume()
    {
        if (Availability == Availability.Consumed) throw new InvalidOperationException("The item is already consumed.");
        Quantity = 0;
        Availability = Availability.Consumed;
        Touch();
    }
    public void MarkUnavailable()
    {
        if (Availability == Availability.Consumed) throw new InvalidOperationException("A consumed item cannot be unavailable.");
        if (Availability == Availability.Unavailable) throw new InvalidOperationException("The item is already unavailable.");
        Availability = Availability.Unavailable;
        Touch();
    }
    public void Restore(decimal quantity)
    {
        if (Availability != Availability.Consumed) throw new InvalidOperationException("Only consumed items can be restored.");
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        Quantity = quantity;
        Availability = Availability.Available;
        Touch();
    }

    public bool HasSameIdentity(PantryItem other) =>
        other is not null &&
        string.Equals(OwnerId, other.OwnerId, StringComparison.Ordinal) &&
        string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase) &&
        Type == other.Type &&
        string.Equals(CustomCategory, other.CustomCategory, StringComparison.OrdinalIgnoreCase);
    public bool IsDuplicateOf(PantryItem other) => HasSameIdentity(other);
    public bool IsAvailable => Availability == Availability.Available;

    public void Update(
        string name,
        PantryType type,
        string? customCategory,
        string? notes,
        decimal quantity,
        Unit unit,
        DateTimeOffset? expiresAt)
    {
        Type = ValidateType(type);
        SetCustomCategory(customCategory);
        Rename(name);
        SetQuantity(quantity);
        SetUnit(unit);
        SetNotes(notes);
        SetExpiration(expiresAt);
        Touch();
    }

    private static PantryType ValidateType(PantryType type)
    {
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        return type;
    }
    private static Unit ValidateUnit(Unit unit)
    {
        if (!Enum.IsDefined(unit)) throw new ArgumentOutOfRangeException(nameof(unit));
        return unit;
    }
    private static string NormalizeRequired(string value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A value is required.", parameterName);
        var normalized = string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length > maxLength) throw new ArgumentException($"Value must be no more than {maxLength} characters.", parameterName);
        return normalized;
    }
    private void Touch() { UpdatedAt = DateTimeOffset.UtcNow; }
}
