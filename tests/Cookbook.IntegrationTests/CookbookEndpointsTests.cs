using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cookbook.IntegrationTests.Support;

namespace Cookbook.IntegrationTests;

public sealed class CookbookEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Folders = "/api/v1/folders";
    private readonly HttpClient client = factory.CreateClient();
    private sealed record Caller(string Id, string Token);

    private Caller NewCaller(string? scope = "cookbook.read cookbook.write")
    {
        var id = Guid.NewGuid().ToString();
        return new(id, factory.Jwt.Create(id, scope));
    }

    private async Task<HttpResponseMessage> SendAsync(Caller? caller, HttpMethod method, string url, object? body = null)
    {
        using var request = factory.Request(method, url, caller?.Token, body);
        return await client.SendAsync(request);
    }

    private static async Task<Guid> FolderIdAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

    [Fact]
    public async Task Health_is_anonymous()
    {
        using var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_caller_can_complete_the_folder_and_saved_recipe_lifecycle()
    {
        var caller = NewCaller();
        using var create = await SendAsync(caller, HttpMethod.Post, Folders, new { name = "  Favorites  " });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.NotNull(create.Headers.Location);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var folderId = created.GetProperty("id").GetGuid();
        Assert.Equal("Favorites", created.GetProperty("name").GetString());

        using var list = await SendAsync(caller, HttpMethod.Get, Folders);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var folders = await list.Content.ReadFromJsonAsync<JsonElement[]>() ?? [];
        Assert.Contains(folders, folder => folder.GetProperty("id").GetGuid() == folderId);

        var recipeId = Guid.NewGuid();
        using var save = await SendAsync(caller, HttpMethod.Post, $"{Folders}/{folderId}/saved-recipes", new { recipeId, recipeVersion = 2 });
        Assert.Equal(HttpStatusCode.Created, save.StatusCode);
        var saved = await save.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(recipeId, saved.GetProperty("recipeId").GetGuid());
        Assert.Equal(2, saved.GetProperty("recipeVersion").GetInt32());

        using var savedList = await SendAsync(caller, HttpMethod.Get, $"{Folders}/{folderId}/saved-recipes");
        Assert.Equal(HttpStatusCode.OK, savedList.StatusCode);
        Assert.Single(await savedList.Content.ReadFromJsonAsync<JsonElement[]>() ?? []);
        using var remove = await SendAsync(caller, HttpMethod.Delete, $"{Folders}/{folderId}/saved-recipes/{recipeId}/2");
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);
        using var delete = await SendAsync(caller, HttpMethod.Delete, $"{Folders}/{folderId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Invalid_folder_name_returns_bad_request(string name)
    {
        using var response = await SendAsync(NewCaller(), HttpMethod.Post, Folders, new { name });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", 1)]
    [InlineData("00000000-0000-0000-0000-000000000001", 0)]
    public async Task Invalid_recipe_reference_returns_bad_request(string recipeId, int version)
    {
        var caller = NewCaller();
        using var create = await SendAsync(caller, HttpMethod.Post, Folders, new { name = "Favorites" });
        var folderId = await FolderIdAsync(create);
        using var response = await SendAsync(caller, HttpMethod.Post, $"{Folders}/{folderId}/saved-recipes", new { recipeId, recipeVersion = version });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var routeResponse = await SendAsync(caller, HttpMethod.Delete, $"{Folders}/{folderId}/saved-recipes/{recipeId}/{version}");
        Assert.Equal(HttpStatusCode.BadRequest, routeResponse.StatusCode);
    }

    [Fact]
    public async Task Another_user_cannot_access_any_folder_child_route()
    {
        var owner = NewCaller();
        using var create = await SendAsync(owner, HttpMethod.Post, Folders, new { name = "Private" });
        var folderId = await FolderIdAsync(create);
        var other = NewCaller();
        var recipeId = Guid.NewGuid();
        using var delete = await SendAsync(other, HttpMethod.Delete, $"{Folders}/{folderId}");
        using var save = await SendAsync(other, HttpMethod.Post, $"{Folders}/{folderId}/saved-recipes", new { recipeId, recipeVersion = 1 });
        using var list = await SendAsync(other, HttpMethod.Get, $"{Folders}/{folderId}/saved-recipes");
        using var remove = await SendAsync(other, HttpMethod.Delete, $"{Folders}/{folderId}/saved-recipes/{recipeId}/1");
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, save.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, remove.StatusCode);
    }
}

public sealed class CookbookAuthenticationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient client = factory.CreateClient();
    private const string Folders = "/api/v1/folders";

    private async Task<HttpStatusCode> GetFoldersAsync(string? token)
    {
        using var request = factory.Request(HttpMethod.Get, Folders, token);
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    [Fact] public async Task Missing_token_is_401() => Assert.Equal(HttpStatusCode.Unauthorized, await GetFoldersAsync(null));
    [Fact] public async Task Malformed_token_is_401() => Assert.Equal(HttpStatusCode.Unauthorized, await GetFoldersAsync("not-a-jwt"));
    [Fact] public async Task Invalid_signature_is_401() => Assert.Equal(HttpStatusCode.Unauthorized, await GetFoldersAsync(factory.Jwt.Create(Guid.NewGuid().ToString(), signWithUnknownKey: true)));
    [Fact] public async Task Expired_token_is_401() => Assert.Equal(HttpStatusCode.Unauthorized, await GetFoldersAsync(factory.Jwt.Create(Guid.NewGuid().ToString(), lifetime: TimeSpan.FromMinutes(-10))));
    [Fact] public async Task Wrong_issuer_is_401() => Assert.Equal(HttpStatusCode.Unauthorized, await GetFoldersAsync(factory.Jwt.Create(Guid.NewGuid().ToString(), issuer: "https://evil.example")));
    [Fact] public async Task Wrong_audience_is_401() => Assert.Equal(HttpStatusCode.Unauthorized, await GetFoldersAsync(factory.Jwt.Create(Guid.NewGuid().ToString(), audience: "another-api")));
    [Fact] public async Task Missing_scope_is_403() => Assert.Equal(HttpStatusCode.Forbidden, await GetFoldersAsync(factory.Jwt.Create(Guid.NewGuid().ToString(), scope: null)));
    [Fact] public async Task Unrelated_scope_is_403() => Assert.Equal(HttpStatusCode.Forbidden, await GetFoldersAsync(factory.Jwt.Create(Guid.NewGuid().ToString(), scope: "users.read")));
    [Fact] public async Task Required_scope_is_accepted() => Assert.Equal(HttpStatusCode.OK, await GetFoldersAsync(factory.Jwt.Create(Guid.NewGuid().ToString(), scope: "cookbook.read")));
}
