using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using ChopChop.Shared;
using InteractiveReview.Api.Contracts;
using InteractiveReview.Api.Services;
using InteractiveReview.Data;
using InteractiveReview.Data.Context;
using InteractiveReview.Data.Messaging;
using InteractiveReview.Data.Repositories;
using InteractiveReview.Domain;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration).WriteTo.Console());
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        if (context.HttpContext.Items[CorrelationIdMiddleware.ItemName] is string correlationId)
            context.ProblemDetails.Extensions["correlationId"] = correlationId;
    };
});
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddCors(options =>
{
    var clientOrigin = builder.Configuration["Client:Origin"]
        ?? throw new InvalidOperationException("Client:Origin is required.");
    options.AddDefaultPolicy(policy => policy.WithOrigins(clientOrigin).AllowAnyHeader().AllowAnyMethod());
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("review-submissions", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirstValue("sub") ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
builder.Services.AddInteractiveReviewData(builder.Configuration);
builder.Services.AddSingleton<IEventBus, RabbitMqEventBus>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ReviewApplicationService>();
if (builder.Configuration.GetValue("BackgroundServices:Enabled", true))
    builder.Services.AddHostedService<OutboxDispatcher>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("review:create", policy => policy.RequireAuthenticatedUser());
    options.AddPolicy("review:read", policy => policy.RequireAuthenticatedUser());
    options.AddPolicy("review:moderate", policy =>
        policy.RequireAuthenticatedUser().RequireClaim("role", "review-moderator"));
});
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Auth:Authority"]
            ?? throw new InvalidOperationException("Auth:Authority is required.");
        options.Audience = builder.Configuration["Auth:Audience"]
            ?? throw new InvalidOperationException("Auth:Audience is required.");
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = "preferred_username"
        };
    });

var app = builder.Build();
app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapOpenApi();
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = check => check.Name == "database" })
    .AllowAnonymous();
app.MapGet("/", () => Results.Ok(new { service = "interactive-review", status = "ready" }))
    .AllowAnonymous();
app.MapPost("/api/reviews", async (
        CreateReviewRequest request,
        HttpContext context,
        ReviewApplicationService service,
        CancellationToken cancellationToken) =>
    {
        if (!context.User.TryGetUserId(out var ownerId))
            return Results.Unauthorized();

        try
        {
            var response = await service.CreateAsync(
                ownerId,
                request,
                context.Items[CorrelationIdMiddleware.ItemName]?.ToString() ?? string.Empty,
                cancellationToken);
            return Results.Created($"/api/reviews/{response.Id}", response);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["request"] = [exception.Message]
            });
        }
        catch (DuplicateReviewException)
        {
            return Results.Conflict(new { error = "A review already exists for this recipe and user." });
        }
    })
    .RequireAuthorization("review:create")
    .RequireRateLimiting("review-submissions");
app.MapGet("/api/recipes/{recipeId:guid}/reviews", async (
        Guid recipeId,
        int? page,
        int? pageSize,
        ReviewApplicationService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var reviews = await service.GetPublishedForRecipeAsync(
                recipeId,
                page ?? 1,
                pageSize ?? 20,
                cancellationToken);
            return Results.Ok(reviews);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["pagination"] = [exception.Message]
            });
        }
    })
    .RequireAuthorization("review:read");
app.MapGet("/api/reviews/{id:guid}", async (
        Guid id,
        HttpContext context,
        ReviewApplicationService service,
        CancellationToken cancellationToken) =>
    {
        if (!context.User.TryGetUserId(out var ownerId))
            return Results.Unauthorized();

        var response = await service.GetOwnedAsync(id, ownerId, cancellationToken);
        return response is null ? Results.NotFound() : Results.Ok(response);
    })
    .RequireAuthorization("review:read");
app.MapPut("/api/reviews/{id:guid}", async (
        Guid id,
        UpdateReviewRequest request,
        HttpContext context,
        ReviewApplicationService service,
        CancellationToken cancellationToken) =>
    {
        if (!context.User.TryGetUserId(out var ownerId))
            return Results.Unauthorized();

        try
        {
            return await service.UpdateAsync(
                    id,
                    ownerId,
                    request,
                    context.Items[CorrelationIdMiddleware.ItemName]?.ToString() ?? string.Empty,
                    cancellationToken)
                ? Results.NoContent()
                : Results.NotFound();
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["request"] = [exception.Message]
            });
        }
    })
    .RequireAuthorization("review:create");
app.MapDelete("/api/reviews/{id:guid}", async (
        Guid id,
        HttpContext context,
        ReviewApplicationService service,
        CancellationToken cancellationToken) =>
    {
        if (!context.User.TryGetUserId(out var ownerId))
            return Results.Unauthorized();

        return await service.DeleteAsync(
                id,
                ownerId,
                context.Items[CorrelationIdMiddleware.ItemName]?.ToString() ?? string.Empty,
                cancellationToken)
            ? Results.NoContent()
            : Results.NotFound();
    })
    .RequireAuthorization("review:create");
app.MapGet("/api/moderation/reviews", async (
        int? limit,
        ReviewApplicationService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await service.GetPendingForModerationAsync(limit ?? 50, cancellationToken));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["limit"] = [exception.Message]
            });
        }
    })
    .RequireAuthorization("review:moderate");
app.MapPost("/api/moderation/reviews/{id:guid}", async (
        Guid id,
        ModerateReviewRequest request,
        HttpContext context,
        ReviewApplicationService service,
        CancellationToken cancellationToken) =>
    {
        if (!context.User.TryGetUserId(out var moderatorId))
            return Results.Unauthorized();

        try
        {
            return await service.ModerateAsync(
                id,
                moderatorId,
                request,
                context.Items[CorrelationIdMiddleware.ItemName]?.ToString() ?? string.Empty,
                cancellationToken)
                ? Results.NoContent()
                : Results.NotFound();
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["request"] = [exception.Message]
            });
        }
        catch (InvalidOperationException exception)
        {
            return Results.Conflict(new { error = exception.Message });
        }
    })
    .RequireAuthorization("review:moderate");
app.Run();

public sealed class DatabaseHealthCheck(InteractiveReviewDbContext dbContext)
    : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext healthContext,
        CancellationToken cancellationToken = default)
    {
        var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
        return canConnect
            ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy()
            : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Database is unavailable.");
    }

}

public partial class Program
{
}
