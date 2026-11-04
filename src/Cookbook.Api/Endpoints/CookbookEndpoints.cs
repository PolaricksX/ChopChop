using Cookbook.Api.Contracts;
using Cookbook.Api.Infrastructure;
using Cookbook.Api.Services;

namespace Cookbook.Api.Endpoints;

public static class CookbookEndpoints
{
    public static IEndpointRouteBuilder MapCookbookEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var folders = endpoints.MapGroup("/api/v1/folders");

        folders.MapPost("", async (CreateFolderRequest request, ICurrentUser currentUser, CookbookApplicationService service, CancellationToken cancellationToken) =>
        {
            var response = await service.CreateFolderAsync(currentUser.UserId, request, cancellationToken);
            return Results.Created($"/api/v1/folders/{response.Id}", response);
        }).RequireAuthorization(Policies.CookbookWrite);

        folders.MapGet("", async (ICurrentUser currentUser, CookbookApplicationService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetFoldersAsync(currentUser.UserId, cancellationToken)))
            .RequireAuthorization(Policies.CookbookRead);

        folders.MapDelete("/{folderId:guid}", async (Guid folderId, ICurrentUser currentUser, CookbookApplicationService service, CancellationToken cancellationToken) =>
        {
            await service.DeleteFolderAsync(currentUser.UserId, folderId, cancellationToken);
            return Results.NoContent();
        }).RequireAuthorization(Policies.CookbookWrite);

        folders.MapPost("/{folderId:guid}/saved-recipes", async (Guid folderId, SaveRecipeRequest request, ICurrentUser currentUser, CookbookApplicationService service, CancellationToken cancellationToken) =>
        {
            var response = await service.AddSavedRecipeAsync(currentUser.UserId, folderId, request, cancellationToken);
            return Results.Created($"/api/v1/folders/{folderId}/saved-recipes/{response.RecipeId}/{response.RecipeVersion}", response);
        }).RequireAuthorization(Policies.CookbookWrite);

        folders.MapGet("/{folderId:guid}/saved-recipes", async (Guid folderId, ICurrentUser currentUser, CookbookApplicationService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetSavedRecipesAsync(currentUser.UserId, folderId, cancellationToken)))
            .RequireAuthorization(Policies.CookbookRead);

        folders.MapDelete("/{folderId:guid}/saved-recipes/{recipeId:guid}/{recipeVersion:int}", async (Guid folderId, Guid recipeId, int recipeVersion, ICurrentUser currentUser, CookbookApplicationService service, CancellationToken cancellationToken) =>
        {
            await service.RemoveSavedRecipeAsync(currentUser.UserId, folderId, recipeId, recipeVersion, cancellationToken);
            return Results.NoContent();
        }).RequireAuthorization(Policies.CookbookWrite);

        return endpoints;
    }
}
