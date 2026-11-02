using RecipeManagement.Domain.Enums;

namespace RecipeManagement.Api.Contracts;

public static class RecipeRequestValidator
{
    public static void Validate(RecipeContentRequest request)
    {
        var errors = new List<string>();
        if (request is null) errors.Add("Request body is required.");
        else
        {
            if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200) errors.Add("Title is required and must be at most 200 characters.");
            if (request.Description?.Length > 5_000) errors.Add("Description must be at most 5000 characters.");
            if (request.Servings <= 0) errors.Add("Servings must be positive.");
            if (request.Ingredients is null || request.Ingredients.Count is < 1 or > 100) errors.Add("Ingredients must contain 1 to 100 items.");
            else foreach (var item in request.Ingredients) { if (string.IsNullOrWhiteSpace(item.Name) || item.Name.Length > 200) errors.Add("Ingredient names must be 1 to 200 characters."); if (item.Quantity <= 0) errors.Add("Ingredient quantities must be positive."); if (string.IsNullOrWhiteSpace(item.Unit) || item.Unit.Length > 50) errors.Add("Ingredient units are required and must be at most 50 characters."); }
            if (request.Steps is null || request.Steps.Count is < 1 or > 100) errors.Add("Steps must contain 1 to 100 items.");
            else if (request.Steps.Any(x => x.Order < 1 || string.IsNullOrWhiteSpace(x.Content) || x.Content.Length > 2_000)) errors.Add("Steps must have positive order and content of at most 2000 characters.");
            if (request.AllergenStatus == AllergenStatus.Unknown && request.Allergens?.Count > 0) errors.Add("Unknown allergen status cannot include allergens.");
            if (request.Tags?.Count > 50 || request.DietaryLabels?.Count > 50 || request.Equipment?.Count > 50) errors.Add("Tag, dietary label, and equipment limits were exceeded.");
            if (request.PrepMinutes < 0 || request.CookMinutes < 0 || request.TotalMinutes < 0) errors.Add("Recipe times cannot be negative.");
        }
        if (errors.Count > 0) throw new RequestValidationException(errors);
    }
    public static void Validate(UpdateRecipeRequest request) { if (request is null || request.ExpectedVersion < 1) throw new RequestValidationException(["A positive expected version is required."]); Validate(request.Content); }
    public static void ValidatePage(int page, int pageSize) { if (page is < 1 or > 10_000 || pageSize is < 1 or > 100) throw new RequestValidationException(["Page must be 1..10000 and pageSize must be 1..100."]); }
}

public sealed class RequestValidationException(IReadOnlyList<string> errors) : Exception("The request is invalid.") { public IReadOnlyList<string> Errors { get; } = errors; }
