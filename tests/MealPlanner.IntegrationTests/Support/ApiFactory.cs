using System.Net.Http.Json;
using MealPlanner.Data.Context;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace MealPlanner.IntegrationTests.Support;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string databaseName = $"MealPlannerTests_{Guid.NewGuid():N}";

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
        builder.UseSetting("Auth:DevelopmentAuthentication", "false");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ChopChop"] = ConnectionString,
            ["Auth:Authority"] = TestJwt.Issuer,
            ["Auth:Audience"] = TestJwt.Audience,
            ["RateLimiting:PermitLimit"] = PermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["RabbitMq:Enabled"] = "false",
        }));

        builder.ConfigureServices(services =>
        {
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var configuration = new OpenIdConnectConfiguration { Issuer = TestJwt.Issuer };
                configuration.SigningKeys.Add(Jwt.SigningKey);
                options.Configuration = configuration;
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
            });
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<MealPlannerDbContext>().Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        using (var scope = Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<MealPlannerDbContext>().Database.EnsureDeletedAsync();
        }

        Jwt.Dispose();
        await DisposeAsync();
    }

    public HttpRequestMessage Request(
        HttpMethod method,
        string url,
        string? token = null,
        object? body = null,
        string? idempotencyKey = null,
        string? ifMatch = null,
        string? correlationId = null)
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

        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        if (correlationId is not null)
        {
            request.Headers.Add("X-Correlation-Id", correlationId);
        }

        return request;
    }
}
