using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Pantry.Tests;

public sealed class PantryApiAuthenticationTests : IClassFixture<PantryApiFactory>
{
    private readonly HttpClient client;

    public PantryApiAuthenticationTests(PantryApiFactory factory) => client = factory.CreateClient();

    [Fact]
    public async Task Protected_list_without_test_token_returns_unauthorized()
    {
        var response = await client.GetAsync("/api/v1/pantry/");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Write_with_read_only_test_scope_returns_forbidden()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/pantry/");
        request.Headers.Add("X-Test-User", "auth-test");
        request.Headers.Add("X-Test-Scopes", "pantry.read");
        request.Content = JsonContent.Create(new { Name = "Rice", Type = 0, Quantity = 1, Unit = 0 });

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}

public sealed class PantryApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseEnvironment("Development");
}
