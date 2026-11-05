using System.Text.Json.Serialization;
using ChopChop.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using RecipeManagement.Api.Contracts;
using RecipeManagement.Api.Infrastructure;
using RecipeManagement.Api.Services;
using RecipeManagement.Data;
using RecipeManagement.Data.Repositories;
using RecipeManagement.Domain.Enums;
using RecipeManagement.Domain.Exceptions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddRecipeManagementData(builder.Configuration);
builder.Services.AddCors(options =>
{
    var clientOrigins = builder.Configuration.GetSection("Client:Origins").GetChildren()
        .Select(section => section.Value)
        .Where(origin => origin is not null && !string.IsNullOrWhiteSpace(origin))
        .Select(origin => origin!)
        .ToArray();

    if (clientOrigins.Length == 0 || clientOrigins.Any(string.IsNullOrWhiteSpace))
        throw new InvalidOperationException("Client:Origins must contain at least one origin.");

    options.AddPolicy("recipe-management-client", policy => policy
        .WithOrigins(clientOrigins)
        .WithHeaders("Authorization", "Content-Type", "X-Correlation-Id")
        .WithMethods("GET", "POST", "PUT", "DELETE"));
});
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<RecipeApplicationService>();
builder.Services.AddOptions<AuthOptions>()
    .BindConfiguration("Auth")
    .Validate(AuthOptions.IsValid, "Auth:Authority (absolute URI) and Auth:Audience must be configured.")
    .ValidateOnStart();
builder.Services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearer>();
if (!builder.Services.TryAddDevelopmentAuthentication(builder.Configuration, builder.Environment))
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
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
app.UseCors("recipe-management-client");
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
    var page = RecipeRequestValidator.ParseSearchPage(query["page"].FirstOrDefault(), 1, "page");
    var pageSize = RecipeRequestValidator.ParseSearchPage(query["pageSize"].FirstOrDefault(), 20, "pageSize");
    var excluded = RecipeRequestValidator.ParseSearchEnumList<Allergen>(query["excludedAllergens"].FirstOrDefault(), "excludedAllergens");
    var criteria = new RecipeSearchCriteria(
        RecipeRequestValidator.ParseSearchText(query["text"].FirstOrDefault()),
        RecipeRequestValidator.ParseSearchList(query["tags"].FirstOrDefault(), "tags"),
        RecipeRequestValidator.ParseSearchList(query["dietaryLabels"].FirstOrDefault(), "dietaryLabels"),
        RecipeRequestValidator.ParseOptionalSearchInt(query["maxTotalMinutes"].FirstOrDefault(), 0, RecipeRequestValidator.MaxSearchTotalMinutes, "maxTotalMinutes"),
        RecipeRequestValidator.ParseOptionalSearchInt(query["servings"].FirstOrDefault(), 1, RecipeRequestValidator.MaxSearchServings, "servings"),
        excluded,
        RecipeRequestValidator.ParseSearchList(query["availableEquipment"].FirstOrDefault(), "availableEquipment"),
        RecipeRequestValidator.ParseSearchSort(query["sort"].FirstOrDefault()),
        RecipeRequestValidator.ParseSearchDirection(query["direction"].FirstOrDefault()),
        page,
        pageSize);
    return Results.Ok(await service.SearchAsync(criteria, ct));
}).AllowAnonymous();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.Run();
