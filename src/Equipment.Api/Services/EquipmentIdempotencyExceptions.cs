namespace Equipment.Api.Services;

public sealed class EquipmentIdempotencyConflictException()
    : Exception("The Idempotency-Key was already used with a different request.");

public sealed class EquipmentIdempotencyResourceMissingException()
    : Exception("The resource created by this Idempotency-Key no longer exists.");
