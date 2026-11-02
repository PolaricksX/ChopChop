using RecipeManagement.Domain.Enums;
using RecipeManagement.Domain.Services;

namespace RecipeManagement.Domain.Entities;

public sealed class RecipeVersion
{
    private readonly List<Ingredient> ingredients = [];
    private readonly List<InstructionStep> instructionSteps = [];
    private readonly List<Allergen> allergens = [];
    private readonly List<string> tags = [];
    private readonly List<string> dietaryLabels = [];
    private readonly List<EquipmentRequirement> equipmentRequirements = [];

    private RecipeVersion()
    {
        Title = Description = string.Empty;
    }

    public RecipeVersion(
        string title,
        string description,
        int servings,
        IEnumerable<Ingredient> ingredients,
        IEnumerable<InstructionStep> instructionSteps,
        AllergenStatus allergenStatus = AllergenStatus.Unknown,
        IEnumerable<Allergen>? allergens = null,
        IEnumerable<string>? tags = null,
        IEnumerable<string>? dietaryLabels = null,
        NutritionEstimate? nutrition = null,
        IEnumerable<EquipmentRequirement>? equipmentRequirements = null,
        int prepMinutes = 0,
        int cookMinutes = 0,
        int totalMinutes = 0,
        bool isAiGenerated = false,
        DateTimeOffset createdAtUtc = default)
    {
        Title = title?.Trim() ?? string.Empty;
        Description = description?.Trim() ?? string.Empty;
        Servings = servings;
        this.ingredients.AddRange(ingredients ?? throw new ArgumentNullException(nameof(ingredients)));
        this.instructionSteps.AddRange(instructionSteps ?? throw new ArgumentNullException(nameof(instructionSteps)));
        this.allergens.AddRange(allergens ?? []);
        AllergenStatus = allergenStatus;
        this.tags.AddRange(Normalize(tags));
        this.dietaryLabels.AddRange(Normalize(dietaryLabels));
        Nutrition = nutrition;
        this.equipmentRequirements.AddRange(equipmentRequirements ?? []);
        PrepMinutes = prepMinutes;
        CookMinutes = cookMinutes;
        TotalMinutes = totalMinutes;
        IsAiGenerated = isAiGenerated;
        if (createdAtUtc == default) throw new ArgumentException("A creation timestamp is required.", nameof(createdAtUtc));
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
    }

    public int Version { get; private set; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public int Servings { get; private set; }
    public IReadOnlyList<Ingredient> Ingredients => ingredients;
    public IReadOnlyList<InstructionStep> InstructionSteps => instructionSteps;
    public IReadOnlyList<Allergen> Allergens => allergens;
    public AllergenStatus AllergenStatus { get; private set; }
    public IReadOnlyList<string> Tags => tags;
    public IReadOnlyList<string> DietaryLabels => dietaryLabels;
    public NutritionEstimate? Nutrition { get; private set; }
    public IReadOnlyList<EquipmentRequirement> EquipmentRequirements => equipmentRequirements;
    public int PrepMinutes { get; private set; }
    public int CookMinutes { get; private set; }
    public int TotalMinutes { get; private set; }
    public bool IsAiGenerated { get; private set; }
    public RecipeStatus Status { get; private set; } = RecipeStatus.Draft;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? PublishedAtUtc { get; private set; }

    internal void MarkPublished(DateTimeOffset publishedAtUtc)
    {
        Status = RecipeStatus.Published;
        PublishedAtUtc = publishedAtUtc.ToUniversalTime();
    }

    internal void MarkUnpublished() => Status = RecipeStatus.Unpublished;

    internal void MarkSuperseded() => Status = RecipeStatus.Superseded;

    internal void AssignVersion(int version)
    {
        if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
        Version = version;
        RecipeValidator.ValidateVersion(this);
    }

    internal void ReplaceContentFrom(RecipeVersion replacement)
    {
        Title = replacement.Title;
        Description = replacement.Description;
        Servings = replacement.Servings;
        ingredients.Clear();
        ingredients.AddRange(replacement.ingredients);
        instructionSteps.Clear();
        instructionSteps.AddRange(replacement.instructionSteps);
        allergens.Clear();
        allergens.AddRange(replacement.allergens);
        AllergenStatus = replacement.AllergenStatus;
        tags.Clear();
        tags.AddRange(replacement.tags);
        dietaryLabels.Clear();
        dietaryLabels.AddRange(replacement.dietaryLabels);
        Nutrition = replacement.Nutrition;
        equipmentRequirements.Clear();
        equipmentRequirements.AddRange(replacement.equipmentRequirements);
        PrepMinutes = replacement.PrepMinutes;
        CookMinutes = replacement.CookMinutes;
        TotalMinutes = replacement.TotalMinutes;
        IsAiGenerated = replacement.IsAiGenerated;
        RecipeValidator.ValidateVersion(this);
    }

    private static IEnumerable<string> Normalize(IEnumerable<string>? values) =>
        (values ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase);
}
