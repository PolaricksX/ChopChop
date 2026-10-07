using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using UserPantry.Api.Auth;
using UserPantry.Application.Abstractions;
using UserPantry.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IExpiringPantryItemsQuery, PantryItemsRepository>();

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

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .AllowAnonymous();

app.Run();

public partial class Program;
