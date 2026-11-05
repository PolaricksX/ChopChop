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
using RecipeManagement.Data.Context;

namespace RecipeManagement.IntegrationTests.Support;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string databaseName = $"RecipeManagementTests_{Guid.NewGuid():N}";
    public TestJwt Jwt { get; } = new();

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
            ["Auth:RequireHttpsMetadata"] = "false",
        }));
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
        await scope.ServiceProvider.GetRequiredService<RecipeManagementDbContext>().Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        using (var scope = Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<RecipeManagementDbContext>().Database.EnsureDeletedAsync();
        Jwt.Dispose();
        await DisposeAsync();
    }

    public HttpRequestMessage Request(HttpMethod method, string url, string? token = null, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (token is not null) request.Headers.Authorization = new("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }
}
