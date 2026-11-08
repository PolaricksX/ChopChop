namespace RecipeManagement.Domain.Entities;

public sealed class Ingredient
{
    private Ingredient() => Name = Unit = string.Empty;

    public Ingredient(string name, decimal quantity, string unit)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Ingredient name is required.", nameof(name));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Ingredient quantity must be positive.");
        if (string.IsNullOrWhiteSpace(unit)) throw new ArgumentException("Ingredient unit is required.", nameof(unit));
        Name = name.Trim();
        Quantity = quantity;
        Unit = unit.Trim();
    }

    public string Name { get; private set; }
    public decimal Quantity { get; private set; }
    public string Unit { get; private set; }
    public int Position { get; private set; }

    internal void AssignPosition(int position)
    {
        if (position < 0) throw new ArgumentOutOfRangeException(nameof(position));
        Position = position;
    }
}
