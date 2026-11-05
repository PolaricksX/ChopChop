namespace Equipment.Domain.Exceptions;

public sealed class EquipmentDomainException(string message, string? parameterName = null) : Exception(message)
{
    public string? ParameterName { get; } = parameterName;
}
