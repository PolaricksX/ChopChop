using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace InteractiveReview.IntegrationTests;

public sealed class AuthenticationTests
{
    [Fact]
    public async Task ReviewEndpoint_RejectsMissingBearerToken()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/moderation/reviews");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ModerationPolicy_RejectsAuthenticatedUserWithoutModeratorRole()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestJwt.Create(Guid.NewGuid()));

        using var response = await client.GetAsync("/api/moderation/reviews");

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    private sealed class ApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Auth:Authority"] = "https://issuer.test",
                    ["Auth:Audience"] = "chopchop-test",
                    ["Client:Origin"] = "https://client.test",
                    ["ConnectionStrings:ChopChop"] = "Server=localhost;Database=ChopChopTest;User Id=test;Password=test;TrustServerCertificate=True",
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
        private const string Issuer = "https://issuer.test";
        private const string Audience = "chopchop-test";
        private static readonly SymmetricSecurityKey SigningKey =
            new(Encoding.UTF8.GetBytes("IntegrationTestOnlySigningKey-32Chars"));

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

        public static string Create(Guid subject)
        {
            var token = new JwtSecurityToken(
                Issuer,
                Audience,
                [new Claim("sub", subject.ToString())],
                DateTime.UtcNow.AddMinutes(-1),
                DateTime.UtcNow.AddMinutes(5),
                new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
