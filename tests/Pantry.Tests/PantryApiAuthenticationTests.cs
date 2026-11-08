using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Pantry.Data.Context;
using Pantry.Domain.Entities;
using Pantry.Domain.Enums;

namespace Pantry.Tests;

public sealed class PantryApiAuthenticationTests : IClassFixture<PantryApiFactory>
{
    private readonly PantryApiFactory factory;
    private readonly HttpClient client;

    public PantryApiAuthenticationTests(PantryApiFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task Missing_token_and_development_test_headers_are_unauthorized()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/pantry/");
        request.Headers.Add("X-Test-User", "owner");
        request.Headers.Add("X-Test-Scopes", "pantry.read");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task Invalid_signature_issuer_audience_expiry_and_malformed_tokens_are_rejected()
    {
        using var otherKey = RSA.Create(2048);
        var tokens = new[]
        {
            "malformed",
            factory.Token(signingKey: new RsaSecurityKey(otherKey)),
            factory.Token(issuer: "https://wrong.example"),
            factory.Token(audience: "wrong-audience"),
            factory.Token(expires: DateTime.UtcNow.AddMinutes(-5)),
        };
        foreach (var token in tokens)
        {
            using var request = Request(HttpMethod.Get, token);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request)).StatusCode);
        }
    }

    [Fact]
    public async Task Valid_scope_and_subject_are_required_per_operation()
    {
        using var read = Request(HttpMethod.Get, factory.Token(scope: "pantry.read"));
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(read)).StatusCode);

        using var noScope = Request(HttpMethod.Get, factory.Token(scope: "other"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(noScope)).StatusCode);

        using var noSubject = Request(HttpMethod.Get, factory.Token(subject: null));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(noSubject)).StatusCode);

        using var invalidSubject = Request(HttpMethod.Get, factory.Token(subject: new string('x', 201)));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(invalidSubject)).StatusCode);

        using var readWrite = Request(HttpMethod.Post, factory.Token(scope: "pantry.read"));
        readWrite.Content = JsonContent.Create(new { Name = "Rice", Type = 0, Quantity = 1, Unit = 0 });
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(readWrite)).StatusCode);

        using var write = Request(HttpMethod.Post, factory.Token(scope: "pantry.write"));
        write.Content = JsonContent.Create(new { Name = "", Type = 0, Quantity = 1, Unit = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(write)).StatusCode);
    }

    [Fact]
    public async Task Health_is_public()
    {
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
    }

    [Fact]
    public async Task OpenApi_is_anonymous_in_development_and_unavailable_in_production()
    {
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/openapi/v1.json")).StatusCode);
        using var production = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        using var productionClient = production.CreateClient();
        productionClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token());
        Assert.Equal(HttpStatusCode.NotFound, (await productionClient.GetAsync("/openapi/v1.json")).StatusCode);
    }

    [Fact]
    public async Task Signed_subjects_with_different_case_or_spaces_cannot_access_anothers_item()
    {
        var owner = $"owner-{Guid.NewGuid():N}";
        var item = new PantryItem(Guid.NewGuid(), owner, "Rice", PantryType.Grains, 1, Unit.Cup);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PantryDbContext>();
            db.PantryItems.Add(item);
            await db.SaveChangesAsync();
        }

        foreach (var other in new[] { owner.ToUpperInvariant(), owner + " ", " " + owner })
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/pantry/{item.Id}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: other, scope: "pantry.write"));
            Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(request)).StatusCode);
        }
    }

    private static HttpRequestMessage Request(HttpMethod method, string token)
    {
        var request = new HttpRequestMessage(method, "/api/v1/pantry/");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}

public sealed class PantryApiFactory : WebApplicationFactory<Program>
{
    private const string Issuer = "https://issuer.example";
    private const string Audience = "pantry-api";
    private readonly RSA rsa = RSA.Create(2048);

    public string Token(
        string? subject = "owner",
        string scope = "pantry.read",
        string issuer = Issuer,
        string audience = Audience,
        DateTime? expires = null,
        SecurityKey? signingKey = null)
    {
        var claims = new List<Claim> { new("scope", scope) };
        if (subject is not null) claims.Add(new Claim("sub", subject));
        var token = new JwtSecurityToken(issuer, audience, claims,
            expires: expires ?? DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(signingKey ?? new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Auth:Authority", Issuer);
        builder.UseSetting("Auth:Audience", Audience);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<PantryDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<PantryDbContext>>();
            services.AddDbContext<PantryDbContext>(options => options.UseInMemoryDatabase("PantryAuthTests"));
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
                configuration.SigningKeys.Add(new RsaSecurityKey(rsa));
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
            });
        });
    }
}
