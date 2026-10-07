namespace UserPantry.Domain;

public sealed class PantryItem
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public string Unit { get; set; } = string.Empty;

    public DateTimeOffset? Expiration { get; set; }
}
