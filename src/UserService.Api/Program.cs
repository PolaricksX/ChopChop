using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Serilog;
using UserService.Api.Endpoints;
using UserService.Api.Infrastructure;
using UserService.Api.Services;
using UserService.Data;
using UserService.Data.Context;

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

builder.Services.AddUserServiceData(sp =>
    sp.GetRequiredService<IConfiguration>().GetConnectionString("UserService")
    ?? throw new InvalidOperationException("ConnectionStrings:UserService is not configured."));

builder.Services.AddOptions<AuthOptions>()
    .BindConfiguration("Auth")
    .Validate(AuthOptions.IsValid, "Auth:Authority (absolute URI) and Auth:Audience must be configured.")
    .ValidateOnStart();
builder.Services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearer>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy(Policies.UsersRead, p => p.RequireAuthenticatedUser().RequireAssertion(c => ScopeClaims.HasScope(c.User, Policies.UsersRead)))
    .AddPolicy(Policies.UsersWrite, p => p.RequireAuthenticatedUser().RequireAssertion(c => ScopeClaims.HasScope(c.User, Policies.UsersWrite)));

builder.Services.AddOptions<CorsOptions>().Configure<IConfiguration>((options, configuration) =>
{
    var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    if (origins.Length > 0)
    {
        options.AddDefaultPolicy(policy => policy
            .WithOrigins(origins)
            .WithMethods("GET", "POST", "PUT", "DELETE")
            .WithHeaders("Authorization", "Content-Type", "Idempotency-Key", "X-Correlation-Id")
            .WithExposedHeaders("Location", "X-Correlation-Id"));
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
        var partitionKey = context.User.FindFirst("sub")?.Value
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
builder.Services.AddScoped<UserProfileService>();

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context => context.AddCorrelation());
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddDbContextCheck<UserServiceDbContext>("database");

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
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "User Service v1"));
}

app.MapHealthChecks("/health").AllowAnonymous();
app.MapUserEndpoints();

app.Run();

public partial class Program;
