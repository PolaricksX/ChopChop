namespace Equipment.Data.Exceptions;

public sealed class EquipmentIdempotencyKeyException()
    : Exception("The idempotency key was already used for a request.");
