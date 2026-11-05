namespace Equipment.Data.Exceptions;

public sealed class EquipmentConcurrencyException()
    : Exception("The equipment item was modified by another request. Reload it and try again.");
