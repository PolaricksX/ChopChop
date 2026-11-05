using Microsoft.EntityFrameworkCore;
using Pantry.Data.Context;
using Pantry.Data.Repositories;
using Pantry.Domain.Entities;
using Pantry.Domain.Enums;

namespace Pantry.Tests;

public sealed class PantryDataTests
{
    [Fact]
    public async Task List_is_owner_scoped_and_has_stable_filtered_pagination()
    {
        await using var db = CreateContext();
        var repository = new PantryRepository(db);
        await repository.AddAsync(Item("owner-a", "Apple", PantryType.Fruits), CancellationToken.None);
        await repository.AddAsync(Item("owner-a", "Apricot", PantryType.Fruits), CancellationToken.None);
        await repository.AddAsync(Item("owner-b", "Avocado", PantryType.Fruits), CancellationToken.None);

        var result = await repository.ListFilteredAsync(
            "owner-a", "a", PantryType.Fruits, Availability.Available, null, 2, 1, CancellationToken.None);

        var item = Assert.Single(result);
        Assert.Equal("Apricot", item.Name);
    }

    [Fact]
    public async Task Duplicate_active_identity_is_rejected_but_consumed_item_can_be_readded()
    {
        await using var db = CreateContext();
        var repository = new PantryRepository(db);
        var first = await repository.AddAsync(Item(" owner ", "  Rice  ", PantryType.Grains), CancellationToken.None);

        await Assert.ThrowsAsync<DuplicatePantryItemException>(() =>
            repository.AddAsync(Item("owner", "Rice", PantryType.Grains), CancellationToken.None));

        Assert.True(await repository.ConsumeAsync("owner", first.Id, null, CancellationToken.None));
        var replacement = await repository.AddAsync(Item("OWNER", "Rice", PantryType.Grains), CancellationToken.None);
        Assert.NotEqual(first.Id, replacement.Id);
    }

    [Fact]
    public async Task Consume_and_delete_cannot_cross_owner_boundary()
    {
        await using var db = CreateContext();
        var repository = new PantryRepository(db);
        var item = await repository.AddAsync(Item("owner-a", "Milk", PantryType.Custom), CancellationToken.None);

        Assert.False(await repository.ConsumeAsync("owner-b", item.Id, null, CancellationToken.None));
        Assert.False(await repository.DeleteAsync("owner-b", item.Id, CancellationToken.None));
        Assert.Equal(Availability.Available, (await repository.GetAsync("owner-a", item.Id, CancellationToken.None))!.Availability);
    }

    [Fact]
    public async Task Update_persists_all_fields_and_rejects_an_active_duplicate()
    {
        await using var db = CreateContext();
        var repository = new PantryRepository(db);
        var item = await repository.AddAsync(Item("owner", "Rice", PantryType.Grains), CancellationToken.None);
        var duplicate = await repository.AddAsync(Item("owner", "Beans", PantryType.Grains), CancellationToken.None);

        Assert.True(await repository.UpdateAsync("owner", item.Id, "Brown Rice", PantryType.Custom, "Baking",
            "for risotto", 4, Unit.Kilogram, DateTimeOffset.UtcNow.AddDays(7), item.Version, CancellationToken.None));
        var saved = await repository.GetAsync("owner", item.Id, CancellationToken.None);
        Assert.Equal("Brown Rice", saved!.Name);
        Assert.Equal(PantryType.Custom, saved.Type);
        Assert.Equal("Baking", saved.CustomCategory);
        Assert.Equal("for risotto", saved.Notes);
        Assert.Equal(4, saved.Quantity);
        Assert.Equal(Unit.Kilogram, saved.Unit);
        Assert.NotNull(saved.ExpiresAt);

        await Assert.ThrowsAsync<DuplicatePantryItemException>(() =>
            repository.UpdateAsync("owner", duplicate.Id, "Brown Rice", PantryType.Custom, "Baking",
                null, 1, Unit.Count, null, duplicate.Version, CancellationToken.None));
    }

    [Fact]
    public void Model_has_filtered_normalized_duplicate_index()
    {
        using var db = CreateContext();
        var index = db.Model.FindEntityType(typeof(PantryItem))!
            .GetIndexes()
            .Single(candidate => candidate.GetDatabaseName() == "UX_PantryItems_ActiveOwnerIngredientType");

        Assert.True(index.IsUnique);
        Assert.Equal("[Availability] = 0", index.GetFilter());
        Assert.Equal(
            ["OwnerIdNormalized", "NameNormalized", "Type"],
            index.Properties.Select(property => property.Name));
    }

    private static PantryItem Item(string owner, string name, PantryType type) =>
        type == PantryType.Custom
            ? new(Guid.NewGuid(), owner, name, "General", 1, Unit.Count)
            : new(Guid.NewGuid(), owner, name, type, 1, Unit.Count);

    private static PantryDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<PantryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
