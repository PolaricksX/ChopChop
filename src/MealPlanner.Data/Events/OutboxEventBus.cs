using MealPlanner.Data.Context;

namespace MealPlanner.Data.Events;

public sealed class OutboxEventBus(MealPlannerDbContext context) : IEventBus
{
    public async Task PublishAsync(MealPlanEvent mealPlanEvent, CancellationToken token) =>
        await context.OutboxMessages.AddAsync(OutboxMessage.Create(mealPlanEvent), token);
}
