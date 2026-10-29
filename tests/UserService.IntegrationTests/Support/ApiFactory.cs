using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using UserService.Data.Context;

namespace UserService.IntegrationTests.Support;

/// <summary>
/// Hosts the API against a throwaway SQL Server database with migrations applied. Uses LocalDB by default;
/// set TEST_SQLSERVER_CONNECTION to point at another instance.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string databaseName = $"UserServiceTests_{Guid.NewGuid():N}";

    public TestJwt Jwt { get; } = new();

    public int PermitLimit { get; init; } = 10_000;

    private string ConnectionString
    {
        get
        {
            var baseConnection = Environment.GetEnvironmentVariable("TEST_SQLSERVER_CONNECTION")
                ?? "Server=(localdb)\\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True";
            return new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = databaseName }.ConnectionString;
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:UserService"] = ConnectionString,
            ["Auth:Authority"] = TestJwt.Issuer,
            ["Auth:Audience"] = TestJwt.Audience,
            ["RateLimiting:PermitLimit"] = PermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
        }));

        // Keep the real bearer handler; only supply the signing keys it would normally download from the issuer.
        builder.ConfigureServices(services => services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            var configuration = new OpenIdConnectConfiguration { Issuer = TestJwt.Issuer };
            configuration.SigningKeys.Add(Jwt.SigningKey);
            options.Configuration = configuration;
            options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
        }));
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<UserServiceDbContext>().Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        using (var scope = Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<UserServiceDbContext>().Database.EnsureDeletedAsync();
        }

        Jwt.Dispose();
        await DisposeAsync();
    }

    public HttpRequestMessage Request(HttpMethod method, string url, string? token = null, object? body = null, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (token is not null)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return request;
    }
}
