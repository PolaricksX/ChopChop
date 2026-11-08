using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ChopChop.Shared;
using Equipment.Api.Contracts;
using Equipment.Data.Exceptions;
using Equipment.Data.Persistence;
using Equipment.Data.Repositories;
using Equipment.Domain.Entities;
using Equipment.Domain.Enums;

namespace Equipment.Api.Services;

public sealed class EquipmentApplicationService(
    IUnitOfWork unitOfWork,
    IEquipmentOutboxWriter outboxWriter,
    TimeProvider clock,
    ILogger<EquipmentApplicationService> logger)
{
    private static readonly JsonSerializerOptions IdempotencyJsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<EquipmentPageResponse> ListAsync(
        string ownerId,
        EquipmentListRequest request,
        CancellationToken token)
    {
        var page = await unitOfWork.CustomRepository<IEquipmentRepository>().ListOwnedAsync(
            ownerId,
            new EquipmentQuery(
                request.Page,
                request.PageSize,
                request.Search,
                request.Type,
                request.Condition,
                request.Availability),
            token);
        return new EquipmentPageResponse(page.Items.Select(ToResponse).ToArray(), page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<EquipmentResponse> CreateAsync(
        string ownerId,
        EquipmentUpsertRequest request,
        string idempotencyKey,
        string correlationId,
        CancellationToken token)
    {
        var records = unitOfWork.Repository<EquipmentIdempotencyRecord>();
        var requestHash = Fingerprint(request);
        var existing = await records.GetAsync(
            record => record.OwnerId == ownerId && record.Key == idempotencyKey,
            token);
        if (existing is not null)
        {
            return await ReplayAsync(ownerId, existing, requestHash, token);
        }

        var now = clock.GetUtcNow();
        var equipment = CreateItem(Guid.NewGuid(), ownerId, request, now);
        var repository = unitOfWork.CustomRepository<IEquipmentRepository>();
        if (await repository.DuplicateExistsAsync(
                ownerId,
                equipment.NormalizedName,
                equipment.Type,
                excludingId: null,
                token))
        {
            throw new EquipmentDuplicateException();
        }

        await repository.AddAsync(equipment, token);
        await records.AddAsync(
            new EquipmentIdempotencyRecord(ownerId, idempotencyKey, requestHash, equipment.Id, now),
            token);
        await AddEventAsync(equipment, "EquipmentAdded", "equipment.added", correlationId, now, token);

        try
        {
            await unitOfWork.SaveChangesAsync(token);
        }
        catch (EquipmentIdempotencyKeyException)
        {
            var concurrentRecord = await records.GetAsync(
                record => record.OwnerId == ownerId && record.Key == idempotencyKey,
                token);
            if (concurrentRecord is null)
            {
                throw;
            }

            return await ReplayAsync(ownerId, concurrentRecord, requestHash, token);
        }

        logger.LogInformation("Equipment item {EquipmentId} created", equipment.Id);
        return ToResponse(equipment);
    }

    public async Task<EquipmentResponse> UpdateAsync(
        string ownerId,
        Guid id,
        EquipmentUpsertRequest request,
        string ifMatch,
        string correlationId,
        CancellationToken token)
    {
        var repository = unitOfWork.CustomRepository<IEquipmentRepository>();
        var equipment = await GetOwnedAsync(repository, ownerId, id, forUpdate: true, token);
        var version = DecodeVersion(ifMatch);
        EnsureCurrentVersion(equipment.RowVersion, version);
        var now = clock.GetUtcNow();
        var changed = equipment.Update(
            request.Name!,
            request.Type!.Value,
            request.Quantity!.Value,
            request.Condition!.Value,
            request.Brand,
            request.Size,
            request.Notes,
            request.Location,
            now);

        if (await repository.DuplicateExistsAsync(
                ownerId,
                equipment.NormalizedName,
                equipment.Type,
                equipment.Id,
                token))
        {
            throw new EquipmentDuplicateException();
        }

        repository.SetExpectedVersion(equipment, version);
        if (changed)
        {
            await AddEventAsync(equipment, "EquipmentUpdated", "equipment.updated", correlationId, now, token);
        }

        await unitOfWork.SaveChangesAsync(token);
        logger.LogInformation("Equipment item {EquipmentId} updated", equipment.Id);
        return ToResponse(equipment);
    }

    public async Task<EquipmentResponse> SetAvailabilityAsync(
        string ownerId,
        Guid id,
        bool isAvailable,
        string ifMatch,
        string correlationId,
        CancellationToken token)
    {
        var repository = unitOfWork.CustomRepository<IEquipmentRepository>();
        var equipment = await GetOwnedAsync(repository, ownerId, id, forUpdate: true, token);
        var version = DecodeVersion(ifMatch);
        EnsureCurrentVersion(equipment.RowVersion, version);
        var oldAvailability = equipment.Availability;
        var now = clock.GetUtcNow();
        var availability = isAvailable
            ? EquipmentAvailability.Available
            : EquipmentAvailability.Unavailable;
        equipment.SetAvailability(availability, now);
        repository.SetExpectedVersion(equipment, version);

        if (oldAvailability != equipment.Availability)
        {
            await AddEventAsync(
                equipment,
                "EquipmentAvailabilityChanged",
                "equipment.availability.changed",
                correlationId,
                now,
                token);
        }

        await unitOfWork.SaveChangesAsync(token);
        logger.LogInformation(
            "Equipment item {EquipmentId} availability changed to {Availability}",
            equipment.Id,
            equipment.Availability);
        return ToResponse(equipment);
    }

    public async Task DeleteAsync(
        string ownerId,
        Guid id,
        string ifMatch,
        string correlationId,
        CancellationToken token)
    {
        var repository = unitOfWork.CustomRepository<IEquipmentRepository>();
        var equipment = await GetOwnedAsync(repository, ownerId, id, forUpdate: true, token);
        var version = DecodeVersion(ifMatch);
        EnsureCurrentVersion(equipment.RowVersion, version);
        repository.SetExpectedVersion(equipment, version);
        var now = clock.GetUtcNow();
        await AddEventAsync(equipment, "EquipmentRemoved", "equipment.removed", correlationId, now, token);
        repository.Delete(equipment);
        await unitOfWork.SaveChangesAsync(token);
        logger.LogInformation("Equipment item {EquipmentId} removed", equipment.Id);
    }

    private async Task<EquipmentResponse> ReplayAsync(
        string ownerId,
        EquipmentIdempotencyRecord record,
        string requestHash,
        CancellationToken token)
    {
        if (!string.Equals(record.RequestHash, requestHash, StringComparison.Ordinal))
        {
            throw new EquipmentIdempotencyConflictException();
        }

        var equipment = await unitOfWork.CustomRepository<IEquipmentRepository>()
            .GetOwnedAsync(record.ResourceId, ownerId, forUpdate: false, token);
        return equipment is null
            ? throw new EquipmentIdempotencyResourceMissingException()
            : ToResponse(equipment);
    }

    private async Task AddEventAsync(
        EquipmentItem equipment,
        string eventType,
        string routingKey,
        string correlationId,
        DateTimeOffset occurredAtUtc,
        CancellationToken token)
    {
        var envelope = new EventEnvelope<EquipmentEventPayload>(
            Guid.NewGuid(),
            eventType,
            1,
            occurredAtUtc.ToUniversalTime(),
            "equipment",
            correlationId,
            new EquipmentEventPayload(
                equipment.Id,
                equipment.OwnerId,
                equipment.Type,
                equipment.Quantity,
                equipment.Condition,
                equipment.Availability));
        await outboxWriter.AddAsync(routingKey, envelope, token);
    }

    private static EquipmentItem CreateItem(
        Guid id,
        string ownerId,
        EquipmentUpsertRequest request,
        DateTimeOffset now) =>
        new(
            id,
            ownerId,
            request.Name!,
            request.Type!.Value,
            request.Quantity!.Value,
            now,
            request.Condition!.Value,
            request.Brand,
            request.Size,
            request.Notes,
            request.Location);

    private static async Task<EquipmentItem> GetOwnedAsync(
        IEquipmentRepository repository,
        string ownerId,
        Guid id,
        bool forUpdate,
        CancellationToken token) =>
        await repository.GetOwnedAsync(id, ownerId, forUpdate, token)
        ?? throw new KeyNotFoundException();

    private static byte[] DecodeVersion(string version) =>
        VersionToken.TryDecode(version, out var decoded)
            ? decoded
            : throw new ArgumentException("A valid ETag is required.", nameof(version));

    private static void EnsureCurrentVersion(byte[] currentVersion, byte[] expectedVersion)
    {
        if (!currentVersion.AsSpan().SequenceEqual(expectedVersion))
        {
            throw new EquipmentConcurrencyException();
        }
    }

    private static string Fingerprint(EquipmentUpsertRequest request) =>
        Convert.ToHexString(SHA256.HashData(
            JsonSerializer.SerializeToUtf8Bytes(request, IdempotencyJsonOptions)));

    private static EquipmentResponse ToResponse(EquipmentItem equipment) =>
        new(
            equipment.Id,
            equipment.Name,
            equipment.Type,
            equipment.Quantity,
            equipment.Brand,
            equipment.Size,
            equipment.Notes,
            equipment.Condition,
            equipment.Location,
            equipment.Availability,
            equipment.CreatedAtUtc,
            equipment.UpdatedAtUtc,
            VersionToken.Encode(equipment.RowVersion));

    private sealed record EquipmentEventPayload(
        Guid EquipmentId,
        string OwnerId,
        EquipmentType Type,
        int Quantity,
        EquipmentCondition Condition,
        EquipmentAvailability Availability);
}
