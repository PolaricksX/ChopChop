namespace MealPlanner.Data.Events;

public interface IEventBus
{
    Task PublishAsync(MealPlanEvent mealPlanEvent, CancellationToken token);
}
