using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using AiRecipeGeneration.Api.Infrastructure;
using ChopChop.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace AiRecipeGeneration.IntegrationTests;

public sealed class AuthenticationTests
{
    [Fact]
    public async Task GenerationEndpoint_RejectsMissingBearerToken()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await PostGenerationAsync(client, null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GenerationEndpoint_RejectsInvalidExpiredWrongIssuerAndWrongAudienceTokens()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var tokens = new[]
        {
            "not-a-jwt",
            TestJwt.Create(Guid.NewGuid().ToString(), scope: Policies.GenerationCreate, signWithUnknownKey: true),
            TestJwt.Create(Guid.NewGuid().ToString(), scope: Policies.GenerationCreate, lifetime: TimeSpan.FromMinutes(-10)),
            TestJwt.Create(Guid.NewGuid().ToString(), scope: Policies.GenerationCreate, issuer: "https://evil.example"),
            TestJwt.Create(Guid.NewGuid().ToString(), scope: Policies.GenerationCreate, audience: "another-api"),
        };

        foreach (var token in tokens)
        {
            using var response = await PostGenerationAsync(client, token);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task GenerationEndpoint_RequiresItsSpecificCreateScope()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var missingScope = await PostGenerationAsync(
            client,
            TestJwt.Create(Guid.NewGuid().ToString(), scope: null));
        using var unrelatedScope = await PostGenerationAsync(
            client,
            TestJwt.Create(Guid.NewGuid().ToString(), scope: "recipe-generations.read"));
        using var requiredScope = await PostGenerationAsync(
            client,
            TestJwt.Create(Guid.NewGuid().ToString(), scope: Policies.GenerationCreate));

        Assert.Equal(HttpStatusCode.Forbidden, missingScope.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, unrelatedScope.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, requiredScope.StatusCode);
    }

    [Fact]
    public async Task GenerationEndpoint_AcceptsAuth0PermissionsClaim()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await PostGenerationAsync(
            client,
            TestJwt.Create(Guid.NewGuid().ToString(), scope: null, permissions: [Policies.GenerationCreate]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Auth0OpaqueSubjectPassesValidatedIdentityResolution()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await PostGenerationAsync(
            client,
            TestJwt.Create("auth0|opaque-subject-123", scope: Policies.GenerationCreate));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public void OpaqueSubjectsMapStablyByIssuerAndGuidSubjectsRemainUnchanged()
    {
        var firstIssuer = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("iss", "https://issuer-one.example/"),
            new Claim("sub", "auth0|opaque-subject-123"),
        ]));
        var sameIdentity = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("iss", "https://issuer-one.example/"),
            new Claim("sub", "auth0|opaque-subject-123"),
        ]));
        var otherIssuer = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("iss", "https://issuer-two.example/"),
            new Claim("sub", "auth0|opaque-subject-123"),
        ]));
        var knownId = Guid.NewGuid();
        var guidSubject = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("iss", "https://issuer-one.example/"),
            new Claim("sub", knownId.ToString()),
        ]));

        Assert.True(firstIssuer.TryGetUserId(out var mapped));
        Assert.True(sameIdentity.TryGetUserId(out var sameMapped));
        Assert.True(otherIssuer.TryGetUserId(out var issuerMapped));
        Assert.True(guidSubject.TryGetUserId(out var unchanged));
        Assert.Equal(mapped, sameMapped);
        Assert.NotEqual(mapped, issuerMapped);
        Assert.Equal(knownId, unchanged);
    }

    [Fact]
    public async Task PublicRootRemainsAnonymousAndOpenApiUsesFallbackAuthorization()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var root = await client.GetAsync("/");
        using var openApi = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, root.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, openApi.StatusCode);
    }

    [Fact]
    public async Task Cors_AllowsOnlyTheConfiguredBrowserOrigin()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/recipe-generations");
        request.Headers.Add("Origin", "https://client.test");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");

        using var response = await client.SendAsync(request);

        Assert.Equal("https://client.test", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    private static async Task<HttpResponseMessage> PostGenerationAsync(HttpClient client, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/recipe-generations")
        {
            Content = JsonContent.Create(new { prompt = "Make dinner" })
        };
        if (token is not null)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        return await client.SendAsync(request);
    }

    private sealed class ApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Auth:Authority"] = TestJwt.Issuer,
                    ["Auth:Audience"] = TestJwt.Audience,
                    ["Client:Origin"] = "https://client.test",
                    ["ConnectionStrings:ChopChop"] = "Server=localhost;Database=ChopChopTest;User Id=test;Password=local-test-only;TrustServerCertificate=True",
                    ["Messaging:ConnectionString"] = "amqp://guest:guest@localhost:5672/",
                    ["BackgroundServices:Enabled"] = "false"
                }));
            builder.ConfigureServices(services =>
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.Authority = null;
                    options.Audience = null;
                    options.RequireHttpsMetadata = false;
                    options.TokenValidationParameters = TestJwt.ValidationParameters;
                }));
        }
    }

    private static class TestJwt
    {
        public const string Issuer = "https://issuer.test";
        public const string Audience = "chopchop-test";
        private static readonly SymmetricSecurityKey SigningKey =
            new(Encoding.UTF8.GetBytes("IntegrationTestOnlySigningKey-32Chars"));
        private static readonly SymmetricSecurityKey UnknownKey =
            new(Encoding.UTF8.GetBytes("UnknownTestSigningKey-32Characters"));

        public static TokenValidationParameters ValidationParameters => new()
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = SigningKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        public static string Create(
            string subject,
            string? scope,
            string? issuer = null,
            string? audience = null,
            TimeSpan? lifetime = null,
            bool signWithUnknownKey = false,
            IEnumerable<string>? permissions = null)
        {
            var claims = new List<Claim> { new("sub", subject) };
            if (scope is not null)
            {
                claims.Add(new Claim("scope", scope));
            }
            if (permissions is not null)
            {
                claims.AddRange(permissions.Select(permission => new Claim("permissions", permission)));
            }

            var now = DateTime.UtcNow;
            var token = new JwtSecurityToken(
                issuer ?? Issuer,
                audience ?? Audience,
                claims,
                now.AddMinutes(-1),
                now.Add(lifetime ?? TimeSpan.FromMinutes(5)),
                new SigningCredentials(signWithUnknownKey ? UnknownKey : SigningKey, SecurityAlgorithms.HmacSha256));
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
