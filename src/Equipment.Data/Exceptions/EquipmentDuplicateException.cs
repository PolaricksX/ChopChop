namespace Equipment.Data.Exceptions;

public sealed class EquipmentDuplicateException()
    : Exception("An equipment item with the same normalized name and type already exists.");
