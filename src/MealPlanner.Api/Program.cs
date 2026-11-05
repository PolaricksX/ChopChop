using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using MealPlanner.Api.Endpoints;
using MealPlanner.Api.Infrastructure;
using MealPlanner.Api.Services;
using MealPlanner.Data;
using MealPlanner.Data.Context;
using MealPlanner.Data.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
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

builder.Services.AddMealPlannerData(provider =>
    provider.GetRequiredService<IConfiguration>().GetConnectionString("ChopChop")
    ?? throw new InvalidOperationException("ConnectionStrings:ChopChop is not configured."));

builder.Services.AddOptions<AuthOptions>()
    .BindConfiguration("Auth")
    .Validate(options => builder.Configuration.GetValue<bool>("Auth:DevelopmentAuthentication") || AuthOptions.IsValid(options), "Auth:Authority (absolute URI) and Auth:Audience must be configured.")
    .ValidateOnStart();
builder.Services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearer>();
if (!ChopChop.Shared.DevelopmentAuthentication.TryAddDevelopmentAuthentication(builder.Services, builder.Configuration, builder.Environment))
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy(Policies.MealPlansRead, policy => policy.RequireAuthenticatedUser()
        .RequireAssertion(context => ScopeClaims.HasScope(context.User, Policies.MealPlansRead)))
    .AddPolicy(Policies.MealPlansWrite, policy => policy.RequireAuthenticatedUser()
        .RequireAssertion(context => ScopeClaims.HasScope(context.User, Policies.MealPlansWrite)));

builder.Services.AddOptions<CorsOptions>().Configure<IConfiguration>((options, configuration) =>
{
    var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    if (origins.Length > 0)
    {
        options.AddDefaultPolicy(policy => policy
            .WithOrigins(origins)
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
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

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IMealPlanService, MealPlanService>();
builder.Services.AddOptions<RabbitMqOptions>().BindConfiguration(RabbitMqOptions.SectionName);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context => context.AddCorrelation());
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddDbContextCheck<MealPlannerDbContext>("database");

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
app.MapMealPlanEndpoints();

app.Run();

public partial class Program;
