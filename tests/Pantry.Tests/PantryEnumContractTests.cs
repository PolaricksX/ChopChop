using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pantry.Api.Contracts;
using Pantry.Api.Infrastructure;
using Pantry.Domain.Entities;

namespace Pantry.Tests;

public sealed class PantryEnumContractTests : IClassFixture<PantryApiFactory>
{
    private readonly PantryApiFactory factory;

    public PantryEnumContractTests(PantryApiFactory factory) => this.factory = factory;

    [Fact]
    public async Task Http_contract_accepts_enum_names_and_returns_names_for_create_and_list()
    {
        var store = new ContractStore();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPantryStore>();
            services.AddSingleton<IPantryStore>(store);
        }));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(scope: "pantry.read pantry.write"));

        var create = await client.PostAsJsonAsync("/api/v1/pantry/", new
        {
            name = "Rice",
            type = "Grains",
            quantity = 2,
            unit = "Cup",
            expiresAt = "2026-12-01T00:00:00Z"
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        using var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        Assert.Equal("Grains", created.RootElement.GetProperty("type").GetString());
        Assert.Equal("Cup", created.RootElement.GetProperty("unit").GetString());
        Assert.Equal("Available", created.RootElement.GetProperty("availability").GetString());
        Assert.Equal(DateTimeOffset.Parse("2026-12-01T00:00:00Z"), created.RootElement.GetProperty("expiresAt").GetDateTimeOffset());
        var id = created.RootElement.GetProperty("id").GetGuid();
        var version = created.RootElement.GetProperty("version").GetString();

        using var list = JsonDocument.Parse(await client.GetStringAsync("/api/v1/pantry/?type=Grains&availability=Available"));
        Assert.Equal("Grains", list.RootElement[0].GetProperty("type").GetString());
        Assert.Equal("Cup", list.RootElement[0].GetProperty("unit").GetString());
        Assert.Equal("Available", list.RootElement[0].GetProperty("availability").GetString());

        using var update = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/pantry/{id}");
        update.Headers.TryAddWithoutValidation("If-Match", version);
        update.Content = JsonContent.Create(new { name = "Rice", type = "Grains", quantity = 3, unit = "Kilogram", expiresAt = (string?)null });
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(update)).StatusCode);
        Assert.Equal(Pantry.Domain.Enums.Unit.Kilogram, store.LastUpdate?.Unit);
    }

    [Fact]
    public async Task Http_contract_rejects_unknown_enum_names()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(scope: "pantry.write"));
        var response = await client.PostAsJsonAsync("/api/v1/pantry/", new
        {
            name = "Rice",
            type = "NotAType",
            quantity = 2,
            unit = "Cup"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed class ContractStore : IPantryStore
    {
        private PantryItem? item;
        public PantryRequest? LastUpdate { get; private set; }

        public Task<IReadOnlyList<PantryItem>> ListAsync(string owner, PantryQuery query, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<PantryItem>>(item is null ? [] : [item]);

        public Task<PantryItem> AddAsync(PantryItem value, CancellationToken token)
        {
            item = value;
            return Task.FromResult(value);
        }

        public Task<bool> UpdateAsync(string owner, Guid id, PantryRequest request, byte[] version, CancellationToken token)
        {
            LastUpdate = request;
            return Task.FromResult(true);
        }

        public Task<bool> ExistsAsync(string owner, Guid id, CancellationToken token) => Task.FromResult(item?.Id == id);
        public Task<bool> ChangeAsync(string owner, Guid id, byte[]? version, bool consume, CancellationToken token) => Task.FromResult(false);
        public Task<bool> DeleteAsync(string owner, Guid id, CancellationToken token) => Task.FromResult(false);
    }
}
