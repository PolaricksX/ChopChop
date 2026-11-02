using RecipeManagement.Domain.Enums;
using RecipeManagement.Domain.Exceptions;
using RecipeManagement.Domain.Services;

namespace RecipeManagement.Domain.Entities;

public sealed class Recipe
{
    private readonly List<RecipeVersion> versions = [];

    private Recipe() { }

    private Recipe(Guid id, Guid ownerId, RecipeVersion initialVersion, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty) throw new RecipeValidationException("A recipe identifier is required.");
        RecipeValidator.ValidateOwner(ownerId);
        Id = id;
        OwnerId = ownerId;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        UpdatedAtUtc = CreatedAtUtc;
        initialVersion.AssignVersion(1);
        versions.Add(initialVersion);
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public RecipeStatus Status { get; private set; } = RecipeStatus.Draft;
    public int CurrentVersion => versions.Count == 0 ? 0 : versions.Max(x => x.Version);
    public int? PublishedVersion => Published?.Version;
    public bool HasDraft => versions.Any(x => x.Status == RecipeStatus.Draft);
    public AllergenStatus AllergenStatus => CurrentContent.AllergenStatus;
    public IReadOnlyList<Allergen> Allergens => CurrentContent.Allergens;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public IReadOnlyList<RecipeVersion> Versions => versions;
    public RecipeVersion? Draft => versions.SingleOrDefault(x => x.Status == RecipeStatus.Draft);
    public RecipeVersion? Published => versions.Where(x => x.Status == RecipeStatus.Published).OrderByDescending(x => x.Version).FirstOrDefault();
    private RecipeVersion CurrentContent => Draft ?? Published ?? versions.OrderByDescending(x => x.Version).FirstOrDefault()
        ?? throw new RecipeLifecycleException("A recipe version is required.");

    public static Recipe Create(Guid ownerId, RecipeVersion initialVersion, DateTimeOffset nowUtc, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), ownerId, initialVersion, nowUtc);

    public RecipeVersion CreateDraftVersion(RecipeVersion content, DateTimeOffset nowUtc)
    {
        RecipeLifecycle.EnsureCanCreateDraft(this);
        if (Status is not (RecipeStatus.Published or RecipeStatus.Unpublished))
            throw new RecipeLifecycleException("Only a published or unpublished recipe can receive an edited draft version.");
        content.AssignVersion(CurrentVersion + 1);
        versions.Add(content);
        UpdatedAtUtc = nowUtc.ToUniversalTime();
        return content;
    }

    public void ReplaceDraftContent(RecipeVersion replacement, DateTimeOffset nowUtc)
    {
        var draft = Draft ?? throw new RecipeLifecycleException("A draft is required before it can be replaced.");
        draft.ReplaceContentFrom(replacement);
        UpdatedAtUtc = nowUtc.ToUniversalTime();
    }

    public void DiscardDraft(DateTimeOffset nowUtc)
    {
        var draft = Draft ?? throw new RecipeLifecycleException("A draft is required before it can be discarded.");
        if (versions.Count == 1)
            throw new RecipeLifecycleException("A never-published recipe cannot discard its only version; delete the recipe instead.");
        versions.Remove(draft);
        UpdatedAtUtc = nowUtc.ToUniversalTime();
    }

    public RecipePublicationType Publish(DateTimeOffset nowUtc)
    {
        var draft = Draft ?? throw new RecipeLifecycleException("A draft is required before publication.");
        var publicationType = Status switch
        {
            RecipeStatus.Draft => RecipePublicationType.FirstPublication,
            RecipeStatus.Unpublished => RecipePublicationType.Republication,
            RecipeStatus.Published => RecipePublicationType.Update,
            _ => throw new RecipeLifecycleException("The recipe cannot be published from its current state.")
        };
        RecipeLifecycle.EnsureTransition(Status, RecipeStatus.Published);
        Published?.MarkSuperseded();
        draft.MarkPublished(nowUtc);
        Status = RecipeStatus.Published;
        UpdatedAtUtc = nowUtc.ToUniversalTime();
        return publicationType;
    }

    public void Unpublish(DateTimeOffset nowUtc)
    {
        RecipeLifecycle.EnsureTransition(Status, RecipeStatus.Unpublished);
        var published = Published ?? throw new RecipeLifecycleException("A published version is required before unpublication.");
        published.MarkUnpublished();
        Status = RecipeStatus.Unpublished;
        UpdatedAtUtc = nowUtc.ToUniversalTime();
    }
}
