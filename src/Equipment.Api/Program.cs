using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;
using Equipment.Api.Endpoints;
using Equipment.Api.Infrastructure;
using Equipment.Api.Services;
using Equipment.Data;
using Equipment.Data.Context;
using Equipment.Data.Messaging;
using Equipment.Data.Repositories;
using Microsoft.AspNetCore.Authorization;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"));

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = 1_048_576;
});

builder.Services.AddEquipmentData(
    builder.Configuration.GetConnectionString("Equipment")
    ?? throw new InvalidOperationException("ConnectionStrings:Equipment is not configured."));
builder.Services.AddOptions<EquipmentMessagingOptions>()
    .BindConfiguration(EquipmentMessagingOptions.SectionName)
    .Validate(EquipmentMessagingOptions.IsValid, "Messaging:ConnectionString must be an AMQP URI when messaging is enabled.")
    .ValidateOnStart();
builder.Services.AddSingleton<ChopChop.Shared.IEventBus, EquipmentRabbitMqEventBus>();
builder.Services.AddHostedService<EquipmentOutboxDispatcher>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<EquipmentApplicationService>();
if (!ChopChop.Shared.DevelopmentAuthentication.AddIfEnabled(
        builder.Services, builder.Configuration, builder.Environment))
{
    builder.Services.AddOptions<AuthOptions>()
        .BindConfiguration(AuthOptions.SectionName)
        .Validate(options => AuthOptions.IsValid(options, builder.Environment.IsDevelopment()),
            "Auth:Authority must be HTTPS outside Development; issuer, audience, HTTPS metadata, and supported algorithms must be valid.")
        .ValidateOnStart();
    builder.Services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearer>();
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
}
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context => context.AddCorrelation());
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddDbContextCheck<EquipmentDbContext>("database");
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy(EquipmentPolicies.Read, policy => policy.RequireAuthenticatedUser()
        .RequireAssertion(context => ScopeClaims.HasScope(context.User, EquipmentPolicies.Read)))
    .AddPolicy(EquipmentPolicies.Write, policy => policy.RequireAuthenticatedUser()
        .RequireAssertion(context => ScopeClaims.HasScope(context.User, EquipmentPolicies.Write)));
builder.Services.AddOptions<CorsOptions>().Configure<IConfiguration>((options, configuration) =>
{
    var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    if (origins.Length > 0)
    {
        options.AddDefaultPolicy(policy => policy
            .WithOrigins(origins)
            .WithMethods("GET", "POST", "PUT", "DELETE")
            .WithHeaders("Authorization", "Content-Type", "Idempotency-Key", "If-Match", "X-Correlation-Id")
            .WithExposedHeaders("ETag", "Location", "X-Correlation-Id"));
    }
});
builder.Services.AddCors();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            return RateLimitPartition.GetNoLimiter("health");
        }

        var settings = context.RequestServices.GetRequiredService<IConfiguration>();
        var partitionKey = context.User.FindFirstValue("sub")
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = settings.GetValue("RateLimiting:PermitLimit", 100),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });
    });
});

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging(options =>
    options.EnrichDiagnosticContext = (diagnostics, context) =>
        diagnostics.Set(CorrelationIdMiddleware.ItemKey, CorrelationIdMiddleware.Get(context)));
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.MapHealthChecks("/health").AllowAnonymous();
app.MapEquipmentEndpoints();

app.Run();

public partial class Program;
