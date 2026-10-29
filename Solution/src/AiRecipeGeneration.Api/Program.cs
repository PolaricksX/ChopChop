using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using AiRecipeGeneration.Data;
using AiRecipeGeneration.Data.Context;
using AiRecipeGeneration.Data.Messaging;
using AiRecipeGeneration.Api.Contracts;
using AiRecipeGeneration.Api.Services;
using ChopChop.Shared;
using AiRecipeGeneration.Data.Providers;
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
    options.AddPolicy("recipe-generations", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirstValue("sub") ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        var correlationId = context.HttpContext.Items[CorrelationIdMiddleware.ItemName]?.ToString();
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            type = "about:blank",
            title = "Request rate limit exceeded.",
            status = StatusCodes.Status429TooManyRequests,
            detail = "Please wait before submitting another recipe generation request.",
            correlationId
        }, cancellationToken);
    };
});
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
builder.Services.AddAiRecipeData(builder.Configuration);
builder.Services.AddSingleton<IEventBus, RabbitMqEventBus>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<GenerationApplicationService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<CorrelationIdDelegatingHandler>();
builder.Services
    .AddHttpClient<IAiRecipeProvider, AzureOpenAiRecipeProvider>(client =>
        client.Timeout = TimeSpan.FromSeconds(60))
    .AddHttpMessageHandler<CorrelationIdDelegatingHandler>();
builder.Services.AddScoped<GenerationProcessor>();
if (builder.Configuration.GetValue("BackgroundServices:Enabled", true))
{
    builder.Services.AddHostedService<OutboxDispatcher>();
    builder.Services.AddHostedService<GenerationConsumer>();
    builder.Services.AddHostedService<GenerationRecoveryWorker>();
}
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
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("recipe:create", policy => policy.RequireAuthenticatedUser());
    options.AddPolicy("recipe:read", policy => policy.RequireAuthenticatedUser());
    options.AddPolicy("recipe:confirm", policy => policy.RequireAuthenticatedUser());
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
app.MapGet("/", () => Results.Ok(new { service = "ai-recipe-generation", status = "ready" }))
    .AllowAnonymous();
app.MapPost("/api/recipe-generations", async (
        CreateGenerationRequest request,
        HttpContext context,
        GenerationApplicationService service,
        CancellationToken cancellationToken) =>
    {
        if (!context.User.TryGetUserId(out var ownerId))
            return Results.Unauthorized();

        var idempotencyKey = context.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Idempotency-Key"] = ["The Idempotency-Key header is required."]
            });
        }

        try
        {
            var response = await service.CreateAsync(
                ownerId,
                idempotencyKey,
                request,
                context.Items[CorrelationIdMiddleware.ItemName]?.ToString() ?? string.Empty,
                cancellationToken);
            return Results.Accepted($"/api/recipe-generations/{response.Id}", response);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["request"] = [exception.Message]
            });
        }
        catch (IdempotencyConflictException)
        {
            return Results.Conflict(new { error = "Idempotency key was already used with a different request." });
        }
    })
    .RequireAuthorization("recipe:create")
    .RequireRateLimiting("recipe-generations");
app.MapGet("/api/recipe-generations/{id:guid}", async (
        Guid id,
        HttpContext context,
        GenerationApplicationService service,
        CancellationToken cancellationToken) =>
    {
        if (!context.User.TryGetUserId(out var ownerId))
            return Results.Unauthorized();

        var response = await service.GetOwnedAsync(id, ownerId, cancellationToken);
        return response is null ? Results.NotFound() : Results.Ok(response);
    })
    .RequireAuthorization("recipe:read");
app.MapPost("/api/recipe-generations/{id:guid}/confirm", async (
        Guid id,
        HttpContext context,
        GenerationApplicationService service,
        CancellationToken cancellationToken) =>
    {
        if (!context.User.TryGetUserId(out var ownerId))
            return Results.Unauthorized();

        try
        {
            return await service.ConfirmAsync(
                    id,
                    ownerId,
                    context.Items[CorrelationIdMiddleware.ItemName]?.ToString() ?? string.Empty,
                    cancellationToken)
                ? Results.NoContent()
                : Results.NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return Results.Conflict(new { error = exception.Message });
        }
    })
    .RequireAuthorization("recipe:confirm");
app.Run();

public sealed class DatabaseHealthCheck(AiRecipeDbContext dbContext) : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
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
