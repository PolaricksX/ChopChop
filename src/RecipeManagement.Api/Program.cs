using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using RecipeManagement.Api.Contracts;
using RecipeManagement.Api.Infrastructure;
using RecipeManagement.Api.Services;
using RecipeManagement.Data;
using RecipeManagement.Data.Repositories;
using RecipeManagement.Domain.Exceptions;
using RecipeManagement.Domain.Enums;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddRecipeManagementData(builder.Configuration);
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<RecipeApplicationService>();
builder.Services.AddOptions<AuthOptions>()
    .BindConfiguration("Auth")
    .Validate(AuthOptions.IsValid, "Auth:Authority (absolute URI) and Auth:Audience must be configured.")
    .ValidateOnStart();
builder.Services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearer>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build())
    .AddPolicy(Policies.RecipesRead, policy =>
        policy.RequireAuthenticatedUser().RequireAssertion(context => ScopeClaims.HasScope(context.User, Policies.RecipesRead)))
    .AddPolicy(Policies.RecipesWrite, policy =>
        policy.RequireAuthenticatedUser().RequireAssertion(context => ScopeClaims.HasScope(context.User, Policies.RecipesWrite)))
    .AddPolicy(Policies.RecipesPublish, policy =>
        policy.RequireAuthenticatedUser().RequireAssertion(context => ScopeClaims.HasScope(context.User, Policies.RecipesPublish)));
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();

var app = builder.Build();
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    var (status, title, detail) = exception switch
    {
        RequestValidationException validation => (400, "Validation failed", string.Join(" ", validation.Errors)),
        KeyNotFoundException => (404, "Resource not found", "The requested resource was not found."),
        RecipeConflictException => (409, "Conflict", exception.Message),
        RecipeLifecycleException => (409, "Conflict", exception.Message),
        RecipeValidationException => (400, "Validation failed", exception.Message),
        ArgumentException => (400, "Validation failed", exception.Message),
        UnauthorizedAccessException => (401, "Unauthorized", "Authentication is required."),
        _ => (500, "Internal server error", "The request could not be completed.")
    };
    context.Response.StatusCode = status;
    context.Response.ContentType = "application/problem+json";
    await context.Response.WriteAsJsonAsync(new { type = $"https://httpstatuses.com/{status}", title, status, detail }, cancellationToken: context.RequestAborted);
}));
app.UseAuthentication();
app.UseAuthorization();

var recipes = app.MapGroup("/api/v1/recipes");
recipes.MapPost("", async (CreateRecipeRequest request, ICurrentUser user, RecipeApplicationService service, HttpContext http, CancellationToken ct) =>
{
    var response = await service.CreateDraftAsync(user.UserId, request, ct);
    return Results.Created($"/api/v1/recipes/{response.RecipeId}/draft", response);
}).RequireAuthorization("recipes.write");
recipes.MapGet("/drafts", async (int? page, int? pageSize, ICurrentUser user, RecipeApplicationService service, CancellationToken ct) =>
    Results.Ok(await service.ListDraftsAsync(user.UserId, page ?? 1, pageSize ?? 20, ct))).RequireAuthorization("recipes.read");
recipes.MapGet("/{recipeId:guid}/draft", async (Guid recipeId, ICurrentUser user, RecipeApplicationService service, CancellationToken ct) =>
    (await service.GetDraftAsync(user.UserId, recipeId, ct)) is { } result ? Results.Ok(result) : Results.NotFound()).RequireAuthorization("recipes.read");
recipes.MapPut("/{recipeId:guid}/draft", async (Guid recipeId, UpdateRecipeRequest request, ICurrentUser user, RecipeApplicationService service, CancellationToken ct) =>
{
    await service.UpdateDraftAsync(user.UserId, recipeId, request, ct); return Results.NoContent();
}).RequireAuthorization("recipes.write");
recipes.MapDelete("/{recipeId:guid}/draft", async (Guid recipeId, ICurrentUser user, RecipeApplicationService service, CancellationToken ct) =>
{
    await service.DeleteDraftAsync(user.UserId, recipeId, ct); return Results.NoContent();
}).RequireAuthorization("recipes.write");
recipes.MapPost("/{recipeId:guid}/publish", async (Guid recipeId, ICurrentUser user, RecipeApplicationService service, CancellationToken ct) => Results.Ok(await service.PublishAsync(user.UserId, recipeId, ct))).RequireAuthorization("recipes.publish");
recipes.MapPost("/{recipeId:guid}/unpublish", async (Guid recipeId, ICurrentUser user, RecipeApplicationService service, CancellationToken ct) => { await service.UnpublishAsync(user.UserId, recipeId, ct); return Results.NoContent(); }).RequireAuthorization("recipes.publish");
recipes.MapGet("/{recipeId:guid}/versions/{version:int}", async (Guid recipeId, int version, RecipeApplicationService service, CancellationToken ct) => (await service.GetPublishedVersionAsync(recipeId, version, ct)) is { } result ? Results.Ok(result) : Results.NotFound()).AllowAnonymous();
recipes.MapGet("/{recipeId:guid}", async (Guid recipeId, RecipeApplicationService service, CancellationToken ct) => (await service.GetPublishedAsync(recipeId, ct)) is { } result ? Results.Ok(result) : Results.NotFound()).AllowAnonymous();
recipes.MapGet("", async (HttpRequest request, RecipeApplicationService service, CancellationToken ct) =>
{
    var query = request.Query;
    var page = int.TryParse(query["page"], out var p) ? p : 1;
    var pageSize = int.TryParse(query["pageSize"], out var ps) ? ps : 20;
    RecipeRequestValidator.ValidatePage(page, pageSize);
    var excluded = ParseEnumList<Allergen>(query["excludedAllergens"]);
    var criteria = new RecipeSearchCriteria(query["text"], ParseList(query["tags"]), ParseList(query["dietaryLabels"]), int.TryParse(query["maxTotalMinutes"], out var max) ? max : null, int.TryParse(query["servings"], out var servings) ? servings : null, excluded, ParseList(query["availableEquipment"]), query["sort"].FirstOrDefault() ?? "updated", !string.Equals(query["direction"], "asc", StringComparison.OrdinalIgnoreCase), page, pageSize);
    return Results.Ok(await service.SearchAsync(criteria, ct));
}).AllowAnonymous();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.Run();

static IReadOnlyList<string>? ParseList(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
static IReadOnlyList<T>? ParseEnumList<T>(string? value) where T : struct, Enum => ParseList(value) is { } values && values.Count > 0 ? values.Select(x => Enum.Parse<T>(x, true)).ToArray() : null;
