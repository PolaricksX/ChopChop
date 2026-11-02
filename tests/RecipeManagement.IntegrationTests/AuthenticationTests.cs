using System.Net;
using RecipeManagement.IntegrationTests.Support;

namespace RecipeManagement.IntegrationTests;

public sealed class AuthenticationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient client = factory.CreateClient();

    public static IEnumerable<object[]> ProtectedRoutes =>
    [
        ["POST", "/api/v1/recipes"],
        ["PUT", "/api/v1/recipes/00000000-0000-0000-0000-000000000001/draft"],
        ["DELETE", "/api/v1/recipes/00000000-0000-0000-0000-000000000001/draft"],
        ["POST", "/api/v1/recipes/00000000-0000-0000-0000-000000000001/publish"],
        ["POST", "/api/v1/recipes/00000000-0000-0000-0000-000000000001/unpublish"],
        ["GET", "/api/v1/recipes/drafts"],
        ["GET", "/api/v1/recipes/00000000-0000-0000-0000-000000000001/draft"],
    ];

    private static object Body() => new
    {
        title = "Tomato soup",
        description = "A simple recipe",
        servings = 2,
        ingredients = new[] { new { name = "Tomato", quantity = 2m, unit = "piece" } },
        steps = new[] { new { order = 1, content = "Cook the tomato." } },
    };

    private async Task<HttpStatusCode> ProtectedRouteAsync(string method, string url, string? token)
    {
        var httpMethod = new HttpMethod(method);
        object? body = method == "POST" && url == "/api/v1/recipes" || method == "PUT" ? Body() : null;
        using var request = factory.Request(httpMethod, url, token, body);
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    private async Task<HttpStatusCode> DraftsAsync(string? token)
    {
        using var request = factory.Request(HttpMethod.Get, "/api/v1/recipes/drafts", token);
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    [Fact] public async Task Missing_token_is_401() => Assert.Equal(HttpStatusCode.Unauthorized, await DraftsAsync(null));
    [Fact] public async Task Malformed_token_is_401() => Assert.Equal(HttpStatusCode.Unauthorized, await DraftsAsync("not-a-jwt"));
    [Fact] public async Task Invalid_signature_is_401() => Assert.Equal(HttpStatusCode.Unauthorized, await DraftsAsync(factory.Jwt.Create("user", signWithUnknownKey: true)));
    [Fact] public async Task Expired_token_is_401() => Assert.Equal(HttpStatusCode.Unauthorized, await DraftsAsync(factory.Jwt.Create("user", lifetime: TimeSpan.FromMinutes(-10))));
    [Fact] public async Task Wrong_issuer_is_401() => Assert.Equal(HttpStatusCode.Unauthorized, await DraftsAsync(factory.Jwt.Create("user", issuer: "https://evil.example")));
    [Fact] public async Task Wrong_audience_is_401() => Assert.Equal(HttpStatusCode.Unauthorized, await DraftsAsync(factory.Jwt.Create("user", audience: "another-api")));
    [Fact] public async Task Missing_scope_is_403() => Assert.Equal(HttpStatusCode.Forbidden, await DraftsAsync(factory.Jwt.Create("user", scope: null)));
    [Fact] public async Task Different_scope_is_403() => Assert.Equal(HttpStatusCode.Forbidden, await DraftsAsync(factory.Jwt.Create("user", scope: "users.read")));
    [Fact] public async Task Required_scope_is_accepted() => Assert.Equal(HttpStatusCode.OK, await DraftsAsync(factory.Jwt.Create(Guid.NewGuid().ToString(), scope: "recipes.read")));
}
