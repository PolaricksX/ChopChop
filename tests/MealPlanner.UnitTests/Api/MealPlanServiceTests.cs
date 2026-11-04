using MealPlanner.Api.Contracts;
using MealPlanner.Api.Services;
using MealPlanner.Data.Context;
using MealPlanner.Data.Events;
using MealPlanner.Data.Repositories;
using MealPlanner.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace MealPlanner.UnitTests.Api;

public sealed class MealPlanServiceTests
{
    [Fact]
    public async Task Create_is_idempotent_and_stages_event_with_plan()
    {
        await using var provider = CreateProvider(Guid.NewGuid().ToString());
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MealPlannerDbContext>();
        var service = CreateService(scope);
        var request = new CreateMealPlanRequest("Week", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 7));

        var first = await service.CreateAsync("owner-1", request, "key-1", "trace-1", CancellationToken.None);
        var replay = await service.CreateAsync("owner-1", request, "key-1", "trace-1", CancellationToken.None);

        Assert.Equal(first.Id, replay.Id);
        Assert.Equal(1, await context.MealPlans.CountAsync());
        Assert.Equal(1, await context.IdempotencyRecords.CountAsync());
        Assert.Equal(1, await context.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Create_with_reused_key_and_different_payload_conflicts()
    {
        await using var provider = CreateProvider(Guid.NewGuid().ToString());
        using var scope = provider.CreateScope();
        var service = CreateService(scope);
        var request = new CreateMealPlanRequest("Week", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 7));
        await service.CreateAsync("owner-1", request, "key-1", "trace-1", CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(
            "owner-1",
            request with { Name = "Another week" },
            "key-1",
            "trace-1",
            CancellationToken.None));
    }

    [Fact]
    public async Task Get_hides_another_users_plan()
    {
        await using var provider = CreateProvider(Guid.NewGuid().ToString());
        using var scope = provider.CreateScope();
        var service = CreateService(scope);
        var plan = await service.CreateAsync(
            "owner-1",
            new CreateMealPlanRequest("Week", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 7)),
            "key-1",
            "trace-1",
            CancellationToken.None);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.GetAsync("owner-2", plan.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Event_failure_does_not_commit_the_plan()
    {
        await using var provider = CreateProvider(Guid.NewGuid().ToString());
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MealPlannerDbContext>();
        var service = new MealPlanService(
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>(),
            new FailingEventBus(),
            TimeProvider.System,
            NullLogger<MealPlanService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(
            "owner-1",
            new CreateMealPlanRequest("Week", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 7)),
            "key-1",
            "trace-1",
            CancellationToken.None));

        Assert.Equal(0, await context.MealPlans.CountAsync());
    }

    [Fact]
    public async Task List_honors_cancellation()
    {
        await using var provider = CreateProvider(Guid.NewGuid().ToString());
        using var scope = provider.CreateScope();
        var service = CreateService(scope);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.ListAsync("owner-1", 1, 20, cancellation.Token));
    }

    private static IMealPlanService CreateService(IServiceScope scope) =>
        new MealPlanService(
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>(),
            scope.ServiceProvider.GetRequiredService<IEventBus>(),
            TimeProvider.System,
            NullLogger<MealPlanService>.Instance);

    private static ServiceProvider CreateProvider(string databaseName)
    {
        var services = new ServiceCollection();
        services.AddDbContext<MealPlannerDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddScoped<IRepositoryFactory, RepositoryFactory>();
        services.AddScoped<IRepositoryProvider, RepositoryProvider>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IMealPlanRepository, MealPlanRepository>();
        services.AddScoped<IRepository<MealPlanner.Domain.Entities.MealPlan>>(
            scope => scope.GetRequiredService<IMealPlanRepository>());
        services.AddScoped<IEventBus, OutboxEventBus>();
        return services.BuildServiceProvider();
    }

    private sealed class FailingEventBus : IEventBus
    {
        public Task PublishAsync(MealPlanEvent mealPlanEvent, CancellationToken token) =>
            throw new InvalidOperationException("Simulated outbox failure.");
    }
}
