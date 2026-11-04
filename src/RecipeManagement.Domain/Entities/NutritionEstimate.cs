namespace RecipeManagement.Domain.Entities;

public sealed class NutritionEstimate
{
    private NutritionEstimate() { }

    public NutritionEstimate(decimal calories, decimal proteinGrams, decimal carbohydratesGrams, decimal fatGrams, decimal fiberGrams, decimal sodiumMilligrams)
    {
        if (calories < 0 || proteinGrams < 0 || carbohydratesGrams < 0 || fatGrams < 0 || fiberGrams < 0 || sodiumMilligrams < 0)
            throw new ArgumentOutOfRangeException(nameof(calories), "Nutrition values cannot be negative.");
        Calories = calories;
        ProteinGrams = proteinGrams;
        CarbohydratesGrams = carbohydratesGrams;
        FatGrams = fatGrams;
        FiberGrams = fiberGrams;
        SodiumMilligrams = sodiumMilligrams;
    }

    public decimal Calories { get; private set; }
    public decimal ProteinGrams { get; private set; }
    public decimal CarbohydratesGrams { get; private set; }
    public decimal FatGrams { get; private set; }
    public decimal FiberGrams { get; private set; }
    public decimal SodiumMilligrams { get; private set; }
}
