using ChopChop.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json.Serialization;
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
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddCors(options => options.AddPolicy("pantry-client", policy =>
    policy.WithOrigins(builder.Configuration["Client:Url"] ?? "http://localhost:5173")
        .AllowAnyHeader().AllowAnyMethod()));

if (!ChopChop.Shared.DevelopmentAuthentication.AddIfEnabled(
        builder.Services, builder.Configuration, builder.Environment))
{
    var authority = builder.Configuration["Auth:Authority"];
    var audience = builder.Configuration["Auth:Audience"];
    if (!Uri.TryCreate(authority, UriKind.Absolute, out var authorityUri)
        || (authorityUri.Scheme != Uri.UriSchemeHttps
            && !(builder.Environment.IsDevelopment() && authorityUri.Scheme == Uri.UriSchemeHttp))
        || string.IsNullOrWhiteSpace(audience))
    {
        throw new InvalidOperationException("A valid Auth:Authority and Auth:Audience must be configured. HTTPS is required outside Development.");
    }

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.Audience = audience;
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.MapInboundClaims = false;
        options.IncludeErrorDetails = false;
        options.TokenValidationParameters.ValidateIssuer = true;
        options.TokenValidationParameters.ValidateAudience = true;
        options.TokenValidationParameters.ValidateLifetime = true;
        options.TokenValidationParameters.RequireExpirationTime = true;
        options.TokenValidationParameters.RequireSignedTokens = true;
        options.TokenValidationParameters.ValidateIssuerSigningKey = true;
        options.TokenValidationParameters.ValidAlgorithms = [SecurityAlgorithms.RsaSha256, SecurityAlgorithms.EcdsaSha256];
    });
}

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser().Build();
    options.AddPolicy("pantry.read", policy => policy.RequireAuthenticatedUser().RequireAssertion(context =>
        HasValidSubject(context.User) &&
        (context.User.Claims.Where(c => c.Type is "scope" or "scp")
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains("pantry.read") ||
        context.User.Claims.Where(c => c.Type is "scope" or "scp")
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains("pantry.write"))));
    options.AddPolicy("pantry.write", policy => policy.RequireAuthenticatedUser().RequireAssertion(context =>
        HasValidSubject(context.User) &&
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
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var status = exception is BadHttpRequestException badRequest
        ? badRequest.StatusCode
        : StatusCodes.Status500InternalServerError;
    context.Response.StatusCode = status;
    context.Response.ContentType = "application/problem+json";
    await Results.Problem(status == StatusCodes.Status500InternalServerError
            ? "An unexpected error occurred."
            : "The request could not be processed.", statusCode: status,
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
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Pantry API v1"));
}

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

static bool HasValidSubject(System.Security.Claims.ClaimsPrincipal user)
{
    var subject = user.FindFirst("sub")?.Value;
    return !string.IsNullOrWhiteSpace(subject) && subject.Length <= 200 && !subject.Any(char.IsControl);
}

public partial class Program { }
