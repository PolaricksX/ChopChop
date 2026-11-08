using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using InteractiveReview.Api.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace InteractiveReview.IntegrationTests;

public sealed class AuthenticationTests
{
    [Fact]
    public async Task ProtectedReviewEndpoint_RejectsMissingBearerToken()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/moderation/reviews");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DevelopmentAuthMode_AllowsProtectedOperationsWithoutBearerTokens()
    {
        using var factory = new ApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Auth:DevelopmentMode", "true");
        });
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/moderation/reviews?limit=101");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedReviewEndpoint_RejectsInvalidExpiredWrongIssuerAndWrongAudienceTokens()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var tokens = new[]
        {
            "not-a-jwt",
            TestJwt.Create(Guid.NewGuid().ToString(), scope: Policies.ReviewsModerate, signWithUnknownKey: true),
            TestJwt.Create(Guid.NewGuid().ToString(), scope: Policies.ReviewsModerate, lifetime: TimeSpan.FromMinutes(-10)),
            TestJwt.Create(Guid.NewGuid().ToString(), scope: Policies.ReviewsModerate, issuer: "https://evil.example"),
            TestJwt.Create(Guid.NewGuid().ToString(), scope: Policies.ReviewsModerate, audience: "another-api"),
        };

        foreach (var token in tokens)
        {
            using var request = factory.Request(HttpMethod.Get, "/api/moderation/reviews", token);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task ModerationPolicy_RequiresItsSpecificScope()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var missingScope = await GetModerationQueueAsync(client, null);
        using var unrelatedScope = await GetModerationQueueAsync(client, "reviews.read");
        using var requiredScope = await GetModerationQueueAsync(client, Policies.ReviewsModerate);

        Assert.Equal(HttpStatusCode.Forbidden, missingScope.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, unrelatedScope.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, requiredScope.StatusCode);
    }

    [Fact]
    public async Task ModerationPolicy_AcceptsAuth0PermissionsClaim()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var request = factory.Request(
            HttpMethod.Get,
            "/api/moderation/reviews?limit=101",
            TestJwt.Create(Guid.NewGuid().ToString(), scope: null, permissions: [Policies.ReviewsModerate]));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReadPolicy_AllowsReadScopeAndRejectsAnUnrelatedScope()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var wrongScopeRequest = factory.Request(
            HttpMethod.Get,
            "/api/recipes/00000000-0000-0000-0000-000000000001/reviews?page=0",
            TestJwt.Create(Guid.NewGuid().ToString(), scope: Policies.ReviewsModerate));
        using var wrongScope = await client.SendAsync(wrongScopeRequest);
        using var readRequest = factory.Request(
            HttpMethod.Get,
            "/api/recipes/00000000-0000-0000-0000-000000000001/reviews?page=0",
            TestJwt.Create(Guid.NewGuid().ToString(), scope: Policies.ReviewsRead));
        using var readScope = await client.SendAsync(readRequest);

        Assert.Equal(HttpStatusCode.Forbidden, wrongScope.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, readScope.StatusCode);
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

    private static async Task<HttpResponseMessage> GetModerationQueueAsync(HttpClient client, string? scope)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/moderation/reviews?limit=101");
        if (scope is not null)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                TestJwt.Create(Guid.NewGuid().ToString(), scope));
        }

        return await client.SendAsync(request);
    }

    private sealed class ApiFactory : WebApplicationFactory<Program>
    {
        public HttpRequestMessage Request(HttpMethod method, string path, string? token)
        {
            var request = new HttpRequestMessage(method, path);
            if (token is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return request;
        }

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
