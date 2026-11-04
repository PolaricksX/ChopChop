namespace MealPlanner.Data;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public bool Enabled { get; init; } = true;

    public string Uri { get; init; } = string.Empty;

    public string Exchange { get; init; } = "chopchop.events";

    public int PollIntervalSeconds { get; init; } = 2;
}
