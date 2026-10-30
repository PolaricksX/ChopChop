using MealPlanner.Data.Context;
using MealPlanner.Data.Events;
using MealPlanner.Data.Repositories;
using MealPlanner.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MealPlanner.UnitTests.Data;

public sealed class UnitOfWorkTests
{
    [Fact]
    public async Task Repository_factory_uses_the_registered_scoped_meal_plan_repository()
    {
        await using var provider = CreateProvider(Guid.NewGuid().ToString());
        using var scope = provider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        Assert.Same(
            unitOfWork.Repository<MealPlan>(),
            unitOfWork.CustomRepository<IMealPlanRepository>());
    }

    [Fact]
    public async Task Repository_stages_changes_until_one_unit_of_work_commit_persists_plan_and_event()
    {
        var databaseName = Guid.NewGuid().ToString();
        await using var provider = CreateProvider(databaseName);
        var now = DateTimeOffset.UtcNow;
        var plan = MealPlan.Create("owner", "Week", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 7), now);

        using (var scope = provider.CreateScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await unitOfWork.Repository<MealPlan>().AddAsync(plan, CancellationToken.None);
        }

        using (var scope = provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MealPlannerDbContext>();
            Assert.Equal(0, await context.MealPlans.CountAsync());
        }

        using (var scope = provider.CreateScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
            await unitOfWork.Repository<MealPlan>().AddAsync(plan, CancellationToken.None);
            await eventBus.PublishAsync(
                new MealPlanEvent(
                    Guid.NewGuid(),
                    "MealPlanChanged",
                    1,
                    now,
                    "MealPlanner",
                    "correlation-1",
                    new { mealPlanId = plan.Id, ownerId = "owner" }),
                CancellationToken.None);
            await unitOfWork.SaveChangesAsync();
        }

        using (var scope = provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MealPlannerDbContext>();
            Assert.Equal(1, await context.MealPlans.CountAsync());
            Assert.Equal(1, await context.OutboxMessages.CountAsync());
        }
    }

    private static ServiceProvider CreateProvider(string databaseName)
    {
        var services = new ServiceCollection();
        services.AddDbContext<MealPlannerDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddScoped<IRepositoryFactory, RepositoryFactory>();
        services.AddScoped<IRepositoryProvider, RepositoryProvider>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IMealPlanRepository, MealPlanRepository>();
        services.AddScoped<IRepository<MealPlan>>(scope => scope.GetRequiredService<IMealPlanRepository>());
        services.AddScoped<IEventBus, OutboxEventBus>();
        return services.BuildServiceProvider();
    }
}
