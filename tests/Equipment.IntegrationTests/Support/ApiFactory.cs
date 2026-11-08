using System.Net.Http.Json;
using Equipment.Data.Context;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Equipment.IntegrationTests.Support;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = $"EquipmentApiTests_{Guid.NewGuid():N}";

    public int PermitLimit { get; set; } = 100;

    public TestJwt Jwt { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting(
            "ConnectionStrings:Equipment",
            "Server=localhost;Database=EquipmentApiTests;Integrated Security=true;TrustServerCertificate=true");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Equipment"] =
                    "Server=localhost;Database=EquipmentApiTests;Integrated Security=true;TrustServerCertificate=true",
                ["Auth:Authority"] = TestJwt.Issuer,
                ["Auth:Issuer"] = TestJwt.Issuer,
                ["Auth:Audience"] = TestJwt.Audience,
                ["Auth:RequireHttpsMetadata"] = "false",
                ["Cors:AllowedOrigins:0"] = "http://localhost:5173",
                ["RateLimiting:PermitLimit"] = PermitLimit.ToString(),
            }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<EquipmentDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<EquipmentDbContext>>();
            services.RemoveAll<EquipmentDbContext>();
            services.AddDbContext<EquipmentDbContext>(options =>
                options.UseInMemoryDatabase(databaseName));

            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var configuration = new OpenIdConnectConfiguration { Issuer = TestJwt.Issuer };
                configuration.SigningKeys.Add(Jwt.SigningKey);
                options.ConfigurationManager =
                    new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
            });
        });
    }

    public HttpRequestMessage Request(
        HttpMethod method,
        string url,
        string? token = null,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (token is not null)
        {
            request.Headers.Authorization = new("Bearer", token);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    public async Task SeedAsync(params Equipment.Domain.Entities.EquipmentItem[] items)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EquipmentDbContext>();
        await context.EquipmentItems.AddRangeAsync(items);
        await context.SaveChangesAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Jwt.Dispose();
        }

        base.Dispose(disposing);
    }
}
