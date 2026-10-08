using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using UserPantry.Api;
using UserPantry.Api.Auth;
using UserPantry.Api.Middleware;
using UserPantry.Application.Abstractions;
using UserPantry.Application.Queries;
using UserPantry.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi("v1");
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "UserPantry API", Version = "v1" });
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<GetExpiringPantryItems>();
builder.Services.AddScoped<IExpiringPantryItemsQuery, PantryItemsRepository>();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services
    .AddAuthentication(DevelopmentAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(
        DevelopmentAuthenticationHandler.SchemeName,
        _ => { });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("pantry.read", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("scope", "pantry.read");
    });
});

builder.Services.AddDbContext<PantryDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("Pantry")
        ?? "Server=localhost,1433;Database=ChopChop;User Id=sa;Password=ChopChop_dev_2026!;TrustServerCertificate=True"));

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "UserPantry API v1");
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .AllowAnonymous();
app.MapPantryItems();

app.Run();

public partial class Program;
