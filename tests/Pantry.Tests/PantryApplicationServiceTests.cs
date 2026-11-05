using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Pantry.Api.Contracts;
using Pantry.Api.Infrastructure;
using Pantry.Api.Services;
using Pantry.Domain.Entities;
using Pantry.Domain.Enums;

namespace Pantry.Tests;

public sealed class PantryApplicationServiceTests
{
    [Fact]
    public async Task List_uses_validated_subject_and_forwards_query_and_cancellation()
    {
        var store = new FakeStore();
        var service = CreateService(store, new Claim("sub", "owner-1"));
        using var cancellation = new CancellationTokenSource();
        var query = new PantryQuery("rice", PantryType.Grains, Availability.Available, null, 2, 5);

        await service.ListAsync(new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "owner-1")], "test")),
            query, cancellation.Token);

        Assert.Equal("owner-1", store.LastOwner);
        Assert.Equal(query, store.LastQuery);
        Assert.Equal(cancellation.Token, store.LastToken);
    }

    [Fact]
    public async Task Add_invalid_request_returns_problem_details_with_correlation_id()
    {
        var service = CreateService(new FakeStore(), new Claim("sub", "owner-1"), "corr-45");

        var result = await service.AddAsync(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "owner-1")], "test")),
            new PantryRequest("", PantryType.Custom, 1, Unit.Count), CancellationToken.None);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider();
        await result.ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Contains("corr-45", await ReadBody(context));
    }

    [Fact]
    public async Task Update_requires_precondition_version()
    {
        var service = CreateService(new FakeStore(), new Claim("sub", "owner-1"));

        var result = await service.UpdateAsync(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "owner-1")], "test")),
            Guid.NewGuid(), new PantryRequest("Rice", PantryType.Grains, 1, Unit.Kilogram), null,
            CancellationToken.None);
        var context = new DefaultHttpContext();
        context.RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider();
        await result.ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status428PreconditionRequired, context.Response.StatusCode);
    }

    [Fact]
    public async Task Missing_subject_is_rejected_before_store_access()
    {
        var store = new FakeStore();
        var service = CreateService(store, new Claim("sub", "owner-1"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ListAsync(
            new ClaimsPrincipal(new ClaimsIdentity()), new PantryQuery(null, null, null, null, 1, 20),
            CancellationToken.None));
        Assert.Null(store.LastOwner);
    }

    private static PantryApplicationService CreateService(FakeStore store, Claim ignored, string correlation = "corr-test")
    {
        var context = new DefaultHttpContext();
        context.TraceIdentifier = correlation;
        return new PantryApplicationService(store, new HttpContextAccessor { HttpContext = context });
    }

    private static async Task<string> ReadBody(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }

    private sealed class FakeStore : IPantryStore
    {
        public string? LastOwner { get; private set; }
        public PantryQuery? LastQuery { get; private set; }
        public CancellationToken LastToken { get; private set; }

        public Task<IReadOnlyList<PantryItem>> ListAsync(string owner, PantryQuery query, CancellationToken token)
        {
            LastOwner = owner;
            LastQuery = query;
            LastToken = token;
            return Task.FromResult<IReadOnlyList<PantryItem>>([]);
        }

        public Task<PantryItem> AddAsync(PantryItem item, CancellationToken token) => Task.FromResult(item);
        public Task<bool> UpdateAsync(string owner, Guid id, PantryRequest request, byte[] version, CancellationToken token) =>
            Task.FromResult(false);
        public Task<bool> ChangeAsync(string owner, Guid id, byte[]? version, bool consume, CancellationToken token) =>
            Task.FromResult(false);
        public Task<bool> ExistsAsync(string owner, Guid id, CancellationToken token) =>
            Task.FromResult(false);
        public Task<bool> DeleteAsync(string owner, Guid id, CancellationToken token) => Task.FromResult(false);
    }
}
