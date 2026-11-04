using System.Net.Http.Json;
using System.Threading;
using Cookbook.Data.Context;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Cookbook.IntegrationTests.Support;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string databaseName = $"CookbookTests_{Guid.NewGuid():N}";
    private readonly SemaphoreSlim initializeGate = new(1, 1);
    private bool databaseInitialized;
    public TestJwt Jwt { get; } = new();

    private string BaseConnectionString => Environment.GetEnvironmentVariable("TEST_SQLSERVER_CONNECTION")
        ?? "Server=(localdb)\\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True";

    private string ConnectionString
    {
        get
        {
            return new SqlConnectionStringBuilder(BaseConnectionString) { InitialCatalog = databaseName }.ConnectionString;
        }
    }

    private string MasterConnectionString => new SqlConnectionStringBuilder(BaseConnectionString)
    {
        InitialCatalog = "master",
    }.ConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ChopChop"] = ConnectionString,
            ["Auth:Authority"] = TestJwt.Issuer,
            ["Auth:Audience"] = TestJwt.Audience,
            ["Auth:RequireHttpsMetadata"] = "false",
            ["Client:Origins:0"] = "https://client.test",
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<CookbookDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<CookbookDbContext>>();
            services.RemoveAll<CookbookDbContext>();
            services.AddDbContext<CookbookDbContext>(options => options.UseSqlServer(ConnectionString));

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
        await initializeGate.WaitAsync();
        try
        {
            if (databaseInitialized)
                return;

            await CreateDatabaseIfMissingAsync();

            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<CookbookDbContext>();
            var configuredDatabase = context.Database.GetDbConnection().Database;
            if (!string.Equals(configuredDatabase, databaseName, StringComparison.Ordinal))
                throw new InvalidOperationException($"Cookbook tests must use '{databaseName}', but resolved '{configuredDatabase}'.");

            await context.Database.MigrateAsync();
            databaseInitialized = true;
        }
        finally
        {
            initializeGate.Release();
        }
    }

    private async Task CreateDatabaseIfMissingAsync()
    {
        await using var connection = new SqlConnection(MasterConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
                              IF DB_ID(@databaseName) IS NULL
                              BEGIN
                                  DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@databaseName);
                                  EXEC(@sql);
                              END
                              """;
        command.Parameters.Add(new SqlParameter("@databaseName", databaseName));
        await command.ExecuteNonQueryAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        await DropDatabaseIfExistsAsync();
        Jwt.Dispose();
        await base.DisposeAsync();
    }

    private async Task DropDatabaseIfExistsAsync()
    {
        if (!databaseName.StartsWith("CookbookTests_", StringComparison.Ordinal))
            throw new InvalidOperationException($"Refusing to drop unexpected test database name: {databaseName}");

        await using var connection = new SqlConnection(MasterConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
                              IF DB_ID(@databaseName) IS NOT NULL
                              BEGIN
                                  DECLARE @setSingleUserSql nvarchar(max) =
                                      N'ALTER DATABASE ' + QUOTENAME(@databaseName) + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE';
                                  DECLARE @dropSql nvarchar(max) = N'DROP DATABASE ' + QUOTENAME(@databaseName);
                                  EXEC(@setSingleUserSql);
                                  EXEC(@dropSql);
                              END
                              """;
        command.Parameters.Add(new SqlParameter("@databaseName", databaseName));
        await command.ExecuteNonQueryAsync();
    }

    public HttpRequestMessage Request(HttpMethod method, string url, string? token = null, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (token is not null) request.Headers.Authorization = new("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }
}
