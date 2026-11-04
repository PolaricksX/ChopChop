namespace RecipeManagement.Domain.Entities;

public sealed class InstructionStep
{
    private InstructionStep() => Content = string.Empty;

    public InstructionStep(int order, string content)
    {
        if (order < 1) throw new ArgumentOutOfRangeException(nameof(order), "Instruction order must start at 1.");
        if (string.IsNullOrWhiteSpace(content)) throw new ArgumentException("Instruction content is required.", nameof(content));
        Order = order;
        Content = content.Trim();
    }

    public int Order { get; private set; }
    public string Content { get; private set; }
}
