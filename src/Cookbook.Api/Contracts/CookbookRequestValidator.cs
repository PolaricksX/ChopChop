using Cookbook.Domain.Exceptions;

namespace Cookbook.Api.Contracts;

public static class CookbookRequestValidator
{
    public const int MaxFolderNameLength = 100;

    public static string ValidateAndNormalize(CreateFolderRequest? request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Name))
            throw new RequestValidationException(["Folder name is required."]);

        var name = request.Name.Trim();
        if (name.Length > MaxFolderNameLength)
            throw new RequestValidationException(["Folder name must be at most 100 characters."]);

        return name;
    }

    public static void Validate(SaveRecipeRequest? request)
    {
        var errors = new List<string>();
        if (request is null)
        {
            errors.Add("Request body is required.");
        }
        else
        {
            if (request.RecipeId == Guid.Empty)
                errors.Add("Recipe ID is required.");
            if (request.RecipeVersion <= 0)
                errors.Add("Recipe version must be positive.");
        }

        if (errors.Count > 0)
            throw new RequestValidationException(errors);
    }

    public static void ValidateRecipeRoute(Guid recipeId, int recipeVersion)
    {
        var errors = new List<string>();
        if (recipeId == Guid.Empty)
            errors.Add("Recipe ID is required.");
        if (recipeVersion <= 0)
            errors.Add("Recipe version must be positive.");
        if (errors.Count > 0)
            throw new RequestValidationException(errors);
    }
}

public sealed class RequestValidationException(IReadOnlyList<string> errors) : Exception("The request is invalid.")
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
