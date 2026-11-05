using System.Text.Json.Serialization;
using ChopChop.Shared;
using Cookbook.Api.Endpoints;
using Cookbook.Api.Infrastructure;
using Cookbook.Api.Services;
using Cookbook.Data;
using Cookbook.Data.Context;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((_, configuration) => configuration.WriteTo.Console());
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddCookbookData(builder.Configuration);
builder.Services.AddCors(options =>
{
    var origins = builder.Configuration.GetSection("Client:Origins").Get<string[]>() ?? [];
    if (origins.Any(string.IsNullOrWhiteSpace))
        throw new InvalidOperationException("Client:Origins must not contain blank entries.");

    options.AddPolicy("cookbook-client", policy => policy
        .WithOrigins(origins)
        .WithHeaders("Authorization", "Content-Type", "X-Correlation-Id")
        .WithMethods("GET", "POST", "DELETE"));
});
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<CookbookApplicationService>();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
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
    .AddPolicy(Policies.CookbookRead, policy => policy.RequireAuthenticatedUser().RequireAssertion(context => ScopeClaims.HasScope(context.User, Policies.CookbookRead)))
    .AddPolicy(Policies.CookbookWrite, policy => policy.RequireAuthenticatedUser().RequireAssertion(context => ScopeClaims.HasScope(context.User, Policies.CookbookWrite)));
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddHealthChecks().AddCheck<CookbookDatabaseHealthCheck>("cookbook-database");
builder.Services.AddOpenApi();

var app = builder.Build();
app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseCors("cookbook-client");
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapCookbookEndpoints();
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.Run();

public partial class Program { }
