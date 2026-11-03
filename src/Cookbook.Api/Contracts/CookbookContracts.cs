namespace Cookbook.Api.Contracts;

public sealed record CreateFolderRequest(string Name);

public sealed record FolderResponse(Guid Id, string Name, DateTimeOffset CreatedAtUtc);

public sealed record SaveRecipeRequest(Guid RecipeId, int RecipeVersion);

public sealed record SavedRecipeResponse(Guid FolderId, Guid RecipeId, int RecipeVersion, DateTimeOffset SavedAtUtc);
