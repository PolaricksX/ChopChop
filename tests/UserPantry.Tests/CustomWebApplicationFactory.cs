using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Net.Sockets;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using UserPantry.Infrastructure;

namespace UserPantry.Tests;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public static readonly Guid DefaultUserId =
        Guid.Parse("00000000-0000-0000-0000-000000000011");

    public static readonly Guid OtherUserId =
        Guid.Parse("00000000-0000-0000-0000-000000000022");

    public DateTimeOffset UtcNow { get; } =
        new(2026, 10, 6, 12, 34, 0, TimeSpan.Zero);

    private string DatabaseName { get; } = $"ChopChopTests_{Guid.NewGuid():N}";
    private bool databaseInitialized;
    private bool disposed;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<PantryDbContext>>();
            services.RemoveAll<TimeProvider>();

            services.AddSingleton<TimeProvider>(new FrozenTimeProvider(UtcNow));
            services.AddDbContext<PantryDbContext>(options =>
                options.UseSqlServer(ConnectionString));

            services.AddAuthentication(TestAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    _ => { });
        });
    }

    public string ConnectionString =>
        $"Server=localhost,1433;Database={DatabaseName};User Id=sa;Password=ChopChop_dev_2026!;TrustServerCertificate=True";

    public async Task InitializeDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PantryDbContext>();
        await db.Database.MigrateAsync();
        databaseInitialized = true;
    }

    public async Task ClearItemsAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PantryDbContext>();
        db.PantryItems.RemoveRange(db.PantryItems);
        await db.SaveChangesAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && databaseInitialized && !disposed)
        {
            disposed = true;
            using var scope = Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<PantryDbContext>()
                .Database.EnsureDeleted();
        }

        base.Dispose(disposing);
    }

    private sealed class FrozenTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}

public sealed class RequiresSqlServerFactAttribute : FactAttribute
{
    public RequiresSqlServerFactAttribute()
    {
        Skip = SqlServerAvailability.IsAvailable
            ? null
            : "SQL Server is required; start Docker Compose before running integration tests.";
    }
}

public sealed class RequiresSqlServerTheoryAttribute : TheoryAttribute
{
    public RequiresSqlServerTheoryAttribute()
    {
        Skip = SqlServerAvailability.IsAvailable
            ? null
            : "SQL Server is required; start Docker Compose before running integration tests.";
    }
}

internal static class SqlServerAvailability
{
    public static bool IsAvailable
    {
        get
        {
            try
            {
                using var client = new TcpClient();
                return client.ConnectAsync("localhost", 1433).Wait(TimeSpan.FromMilliseconds(500));
            }
            catch (SocketException)
            {
                return false;
            }
        }
    }
}

internal sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-User", out var userHeader)
            || !Guid.TryParse(userHeader, out var userId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, "test-user"),
            new Claim("scope", "pantry.read")
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);

        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(principal, SchemeName)));
    }
}
