namespace RecipeManagement.Domain.Entities;

public sealed class EquipmentRequirement
{
    private EquipmentRequirement() => Name = string.Empty;

    public EquipmentRequirement(string name, bool required = true, string? matchingMetadata = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Equipment name is required.", nameof(name));
        Name = name.Trim();
        Required = required;
        MatchingMetadata = string.IsNullOrWhiteSpace(matchingMetadata) ? null : matchingMetadata.Trim();
    }

    public string Name { get; private set; }
    public bool Required { get; private set; }
    public string? MatchingMetadata { get; private set; }
    public int Position { get; private set; }

    internal void AssignPosition(int position)
    {
        if (position < 0) throw new ArgumentOutOfRangeException(nameof(position));
        Position = position;
    }
}
