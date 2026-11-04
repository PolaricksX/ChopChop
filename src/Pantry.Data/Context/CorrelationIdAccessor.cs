namespace Pantry.Data.Context;

public interface ICorrelationIdAccessor
{
    string? Current { get; }
    void Set(string value);
}

public sealed class CorrelationIdAccessor : ICorrelationIdAccessor
{
    private static readonly AsyncLocal<string?> Value = new();

    public string? Current => Value.Value;
    public void Set(string value) => Value.Value = value;
}
