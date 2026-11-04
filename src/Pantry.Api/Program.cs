using System.Security.Claims;
using System.Text.Encodings.Web;
using ChopChop.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pantry.Api.Contracts;
using Pantry.Api.Infrastructure;
using Pantry.Api.Services;
using Pantry.Data.Context;
using Pantry.Data.Repositories;

var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration.GetConnectionString("Pantry")
    ?? "Server=localhost;Database=Pantry;Trusted_Connection=True;TrustServerCertificate=True";

builder.Services.AddDbContext<PantryDbContext>(options => options.UseSqlServer(connection));
builder.Services.AddSingleton<ICorrelationIdAccessor, CorrelationIdAccessor>();
builder.Services.AddScoped<PantryRepository>();
builder.Services.AddScoped<IOutboxStore, OutboxRepository>();
builder.Services.AddScoped<IPantryStore, PantryStore>();
builder.Services.AddScoped<PantryApplicationService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHealthChecks().AddDbContextCheck<PantryDbContext>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddCors(options => options.AddPolicy("pantry-client", policy =>
    policy.WithOrigins(builder.Configuration["Client:Url"] ?? "http://localhost:5173")
        .AllowAnyHeader().AllowAnyMethod()));

var authority = builder.Configuration["Auth:Authority"];
if (!string.IsNullOrWhiteSpace(authority))
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.Audience = builder.Configuration["Auth:Audience"];
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    });
}
else if (builder.Environment.IsDevelopment())
{
    builder.Services.AddAuthentication("Development")
        .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthHandler>("Development", _ => { });
}
else
{
    throw new InvalidOperationException("Auth:Authority must be configured outside Development.");
}

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser().Build();
    options.AddPolicy("pantry.read", policy => policy.RequireAssertion(context =>
        context.User.Claims.Where(c => c.Type is "scope" or "scp")
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains("pantry.read") ||
        context.User.Claims.Where(c => c.Type is "scope" or "scp")
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains("pantry.write")));
    options.AddPolicy("pantry.write", policy => policy.RequireAssertion(context =>
        context.User.Claims.Where(c => c.Type is "scope" or "scp")
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains("pantry.write")));
});
if (!string.IsNullOrWhiteSpace(builder.Configuration["Messaging:ConnectionString"]))
{
    builder.Services.AddSingleton<IEventBus, Pantry.Data.Messaging.RabbitMqEventBus>();
    builder.Services.AddHostedService<Pantry.Api.Services.OutboxDispatcher>();
}
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("pantry", limiter =>
    {
        limiter.PermitLimit = builder.Configuration.GetValue("RateLimiting:PermitLimit", 60);
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var app = builder.Build();
app.UseExceptionHandler(error => error.Run(async context =>
{
    var id = context.TraceIdentifier;
    context.Response.StatusCode = 500;
    context.Response.ContentType = "application/problem+json";
    await Results.Problem("An unexpected error occurred.", statusCode: 500,
        extensions: new Dictionary<string, object?> { ["correlationId"] = id }).ExecuteAsync(context);
}));
app.Use(async (context, next) =>
{
    var correlation = context.Request.Headers["X-Correlation-Id"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(correlation) || correlation.Length > 128)
        correlation = Guid.NewGuid().ToString("N");
    context.TraceIdentifier = correlation;
    context.RequestServices.GetRequiredService<ICorrelationIdAccessor>().Set(correlation);
    context.Response.Headers["X-Correlation-Id"] = correlation;
    using (context.RequestServices.GetRequiredService<ILoggerFactory>()
        .CreateLogger("Request").BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlation }))
        await next();
});
app.UseRateLimiter();
app.UseCors("pantry-client");
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapOpenApi().AllowAnonymous();
if (app.Environment.IsDevelopment())
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Pantry API v1"));

var pantry = app.MapGroup("/api/v1/pantry").RequireRateLimiting("pantry");
pantry.MapGet("/", async (HttpContext http, PantryApplicationService service, CancellationToken token) =>
    await service.ListAsync(http.User, PantryQuery.From(http.Request), token))
    .RequireAuthorization("pantry.read").WithSummary("List pantry items");
pantry.MapPost("/", async (HttpContext http, PantryRequest request, PantryApplicationService service, CancellationToken token) =>
    await service.AddAsync(http.User, request, token))
    .RequireAuthorization("pantry.write").WithSummary("Add a pantry item");
pantry.MapPut("/{id:guid}", async (HttpContext http, Guid id, PantryRequest request, PantryApplicationService service, CancellationToken token) =>
    await service.UpdateAsync(http.User, id, request, http.Request.Headers.IfMatch, token))
    .RequireAuthorization("pantry.write").WithSummary("Update a pantry item");
pantry.MapPost("/{id:guid}/consume", async (HttpContext http, Guid id, PantryApplicationService service, CancellationToken token) =>
    await service.ChangeStateAsync(http.User, id, http.Request.Headers.IfMatch, true, token))
    .RequireAuthorization("pantry.write").WithSummary("Consume a pantry item");
pantry.MapPost("/{id:guid}/unavailable", async (HttpContext http, Guid id, PantryApplicationService service, CancellationToken token) =>
    await service.ChangeStateAsync(http.User, id, http.Request.Headers.IfMatch, false, token))
    .RequireAuthorization("pantry.write").WithSummary("Mark a pantry item unavailable");
pantry.MapDelete("/{id:guid}", async (HttpContext http, Guid id, PantryApplicationService service, CancellationToken token) =>
    await service.DeleteAsync(http.User, id, token))
    .RequireAuthorization("pantry.write").WithSummary("Delete a pantry item");

app.Run();

public sealed class DevelopmentAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var id = Request.Headers["X-Test-User"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(id) || id.Length > 200 || id.Any(char.IsControl))
            return Task.FromResult(AuthenticateResult.NoResult());
        var scopes = Request.Headers["X-Test-Scopes"].FirstOrDefault() ?? string.Empty;
        if (scopes.Any(char.IsControl))
            return Task.FromResult(AuthenticateResult.Fail("Invalid test scope header."));
        var identity = new ClaimsIdentity("Development");
        identity.AddClaim(new Claim("sub", id));
        identity.AddClaim(new Claim("scope", scopes));
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), "Development")));
    }
}

public partial class Program { }
