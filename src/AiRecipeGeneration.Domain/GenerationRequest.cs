namespace AiRecipeGeneration.Domain;

public sealed class GenerationRequest
{
    private GenerationRequest()
    {
        Prompt = string.Empty;
        IdempotencyKey = string.Empty;
        CorrelationId = string.Empty;
    }

    private GenerationRequest(
        Guid id,
        Guid ownerId,
        string idempotencyKey,
        string prompt,
        bool includePantry,
        IReadOnlyList<string> pantryItems,
        string correlationId,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        OwnerId = ownerId;
        IdempotencyKey = idempotencyKey;
        Prompt = prompt;
        IncludePantry = includePantry;
        PantryItems = pantryItems;
        CorrelationId = correlationId;
        CreatedAtUtc = createdAtUtc;
        Status = GenerationStatus.Queued;
        Version = Guid.NewGuid();
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public string IdempotencyKey { get; private set; }
    public string Prompt { get; private set; }
    public bool IncludePantry { get; private set; }
    public IReadOnlyList<string> PantryItems { get; private set; } = [];
    public string CorrelationId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public DateTimeOffset? ProcessingStartedAtUtc { get; private set; }
    public GenerationStatus Status { get; private set; }
    public RecipeDraft? Draft { get; private set; }
    public string? FailureCode { get; private set; }
    public Guid Version { get; private set; }

    public static GenerationRequest Create(
        Guid id,
        Guid ownerId,
        string idempotencyKey,
        string prompt,
        bool includePantry,
        IEnumerable<string>? pantryItems,
        string correlationId,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty || ownerId == Guid.Empty)
            throw new ArgumentException("Request and owner identifiers are required.");

        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 128)
            throw new ArgumentException("The idempotency key must contain 1 to 128 characters.");

        if (string.IsNullOrWhiteSpace(prompt) || prompt.Length > 2_000)
            throw new ArgumentException("The prompt must contain 1 to 2000 characters.");

        if (string.IsNullOrWhiteSpace(correlationId) || correlationId.Length > 128)
            throw new ArgumentException("A correlation identifier of at most 128 characters is required.");

        var pantry = includePantry ? (pantryItems ?? []).ToArray() : [];
        if (pantry.Length > 50 ||
            pantry.Any(item => string.IsNullOrWhiteSpace(item) || item.Length > 120))
        {
            throw new ArgumentException("When included, pantry items must be non-empty and limited to 50 items of at most 120 characters.");
        }
        pantry = pantry.Select(item => item.Trim()).ToArray();

        return new GenerationRequest(
            id,
            ownerId,
            idempotencyKey,
            prompt.Trim(),
            includePantry,
            pantry,
            correlationId,
            createdAtUtc.ToUniversalTime());
    }

    public void MarkProcessing(DateTimeOffset processingAtUtc)
    {
        if (Status != GenerationStatus.Queued)
            throw new InvalidOperationException("Only queued requests can start processing.");

        Status = GenerationStatus.Processing;
        ProcessingStartedAtUtc = processingAtUtc.ToUniversalTime();
        Version = Guid.NewGuid();
    }

    public bool RestartExpiredProcessing(DateTimeOffset nowUtc, TimeSpan lease)
    {
        if (Status != GenerationStatus.Processing ||
            ProcessingStartedAtUtc is null ||
            ProcessingStartedAtUtc > nowUtc.ToUniversalTime() - lease)
        {
            return false;
        }

        ProcessingStartedAtUtc = nowUtc.ToUniversalTime();
        Version = Guid.NewGuid();
        return true;
    }

    public bool RecoverExpiredProcessing(DateTimeOffset nowUtc)
    {
        if (Status != GenerationStatus.Processing || ProcessingStartedAtUtc is null)
            return false;

        Status = GenerationStatus.Queued;
        ProcessingStartedAtUtc = null;
        Version = Guid.NewGuid();
        return true;
    }

    public void Complete(RecipeDraft draft, DateTimeOffset completedAtUtc)
    {
        if (Status != GenerationStatus.Processing)
            throw new InvalidOperationException("Only processing requests can be completed.");

        if (!RecipeDraftValidator.TryValidate(draft, out var error))
            throw new ArgumentException(error, nameof(draft));

        Draft = draft;
        Status = GenerationStatus.Completed;
        CompletedAtUtc = completedAtUtc.ToUniversalTime();
        ProcessingStartedAtUtc = null;
        FailureCode = null;
        Version = Guid.NewGuid();
    }

    public void Fail(string failureCode, DateTimeOffset failedAtUtc)
    {
        if (Status is not (GenerationStatus.Queued or GenerationStatus.Processing))
            throw new InvalidOperationException("Only pending requests can fail.");

        if (string.IsNullOrWhiteSpace(failureCode) || failureCode.Length > 80)
            throw new ArgumentException("A bounded failure code is required.", nameof(failureCode));

        Status = GenerationStatus.Failed;
        FailureCode = failureCode;
        CompletedAtUtc = failedAtUtc.ToUniversalTime();
        ProcessingStartedAtUtc = null;
        Version = Guid.NewGuid();
    }

    public void Confirm(Guid ownerId)
    {
        if (ownerId != OwnerId)
            throw new InvalidOperationException("Only the request owner can confirm a draft.");

        if (Status != GenerationStatus.Completed || Draft is null)
            throw new InvalidOperationException("Only a completed draft can be confirmed.");

        Status = GenerationStatus.Confirmed;
        Version = Guid.NewGuid();
    }
}
