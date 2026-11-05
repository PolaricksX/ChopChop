using Equipment.Data.Context;
using Equipment.Data.Repositories;
using Equipment.Domain.Entities;
using Equipment.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Equipment.UnitTests.Data;

public sealed class EquipmentRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetOwnedAsync_never_returns_another_owners_equipment()
    {
        await using var context = CreateContext();
        var item = Create(ownerId: "Owner-A");
        context.EquipmentItems.Add(item);
        await context.SaveChangesAsync();
        var repository = new EquipmentRepository(context);

        Assert.Null(await repository.GetOwnedAsync(item.Id, "owner-b", forUpdate: false, CancellationToken.None));
        Assert.Null(await repository.GetOwnedAsync(item.Id, "owner-a", forUpdate: false, CancellationToken.None));
        Assert.NotNull(await repository.GetOwnedAsync(item.Id, "Owner-A", forUpdate: false, CancellationToken.None));
    }

    [Fact]
    public async Task ListOwnedAsync_filters_and_paginates_with_stable_ordering()
    {
        await using var context = CreateContext();
        context.EquipmentItems.AddRange(
            Create(ownerId: "owner-a", name: "Whisk", type: EquipmentType.Utensils),
            Create(ownerId: "owner-a", name: "Saucepan", type: EquipmentType.Cookware),
            Create(ownerId: "owner-a", name: "Roasting pan", type: EquipmentType.Cookware),
            Create(ownerId: "owner-b", name: "Another saucepan", type: EquipmentType.Cookware));
        await context.SaveChangesAsync();
        var repository = new EquipmentRepository(context);

        var firstPage = await repository.ListOwnedAsync(
            "owner-a",
            new EquipmentQuery(Page: 1, PageSize: 1, Type: EquipmentType.Cookware),
            CancellationToken.None);
        var secondPage = await repository.ListOwnedAsync(
            "owner-a",
            new EquipmentQuery(Page: 2, PageSize: 1, Type: EquipmentType.Cookware),
            CancellationToken.None);

        Assert.Equal(2, firstPage.TotalCount);
        Assert.Equal("Roasting pan", Assert.Single(firstPage.Items).Name);
        Assert.Equal("Saucepan", Assert.Single(secondPage.Items).Name);
        Assert.Equal(1, firstPage.Page);
        Assert.Equal(1, firstPage.PageSize);
    }

    [Fact]
    public async Task ListOwnedAsync_filters_by_condition_availability_and_search()
    {
        await using var context = CreateContext();
        var match = Create(
            ownerId: "owner-a",
            name: "Chef knife",
            condition: EquipmentCondition.Fair,
            brand: "Acme");
        match.SetAvailability(EquipmentAvailability.Unavailable, Now.AddMinutes(1));
        context.EquipmentItems.AddRange(
            match,
            Create(ownerId: "owner-a", name: "Paring knife", condition: EquipmentCondition.Good),
            Create(ownerId: "owner-b", name: "Chef knife", condition: EquipmentCondition.Fair));
        await context.SaveChangesAsync();
        var repository = new EquipmentRepository(context);

        var page = await repository.ListOwnedAsync(
            "owner-a",
            new EquipmentQuery(
                Search: " ACME ",
                Type: EquipmentType.Cookware,
                Condition: EquipmentCondition.Fair,
                Availability: EquipmentAvailability.Unavailable),
            CancellationToken.None);

        Assert.Equal(1, page.TotalCount);
        Assert.Equal(match.Id, Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task DuplicateExistsAsync_is_owner_and_type_scoped_and_can_exclude_current_item()
    {
        await using var context = CreateContext();
        var item = Create(ownerId: "owner-a", name: "  Chef   knife ");
        context.EquipmentItems.Add(item);
        await context.SaveChangesAsync();
        var repository = new EquipmentRepository(context);

        Assert.True(await repository.DuplicateExistsAsync(
            "owner-a",
            "CHEF KNIFE",
            EquipmentType.Cookware,
            excludingId: null,
            CancellationToken.None));
        Assert.False(await repository.DuplicateExistsAsync(
            "owner-a",
            "CHEF KNIFE",
            EquipmentType.Cookware,
            excludingId: item.Id,
            CancellationToken.None));
        Assert.False(await repository.DuplicateExistsAsync(
            "owner-b",
            "CHEF KNIFE",
            EquipmentType.Cookware,
            excludingId: null,
            CancellationToken.None));
        Assert.False(await repository.DuplicateExistsAsync(
            "owner-a",
            "CHEF KNIFE",
            EquipmentType.Knives,
            excludingId: null,
            CancellationToken.None));
    }

    [Fact]
    public async Task ListOwnedAsync_rejects_invalid_pagination()
    {
        await using var context = CreateContext();
        var repository = new EquipmentRepository(context);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            repository.ListOwnedAsync(
                "owner-a",
                new EquipmentQuery(Page: 0),
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            repository.ListOwnedAsync(
                "owner-a",
                new EquipmentQuery(PageSize: 101),
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            repository.ListOwnedAsync(
                "owner-a",
                new EquipmentQuery(Type: (EquipmentType)999),
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            repository.ListOwnedAsync(
                "owner-a",
                new EquipmentQuery(Search: new string('x', EquipmentQuery.MaxSearchLength + 1)),
                CancellationToken.None));
    }

    [Fact]
    public void Equipment_model_uses_unique_owner_name_type_index_and_exact_owner_collation()
    {
        using var context = CreateContext();
        var entity = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(EquipmentItem));
        var duplicateIndex = Assert.Single(entity!.GetIndexes(), index =>
            index.GetDatabaseName() == "UX_Equipment_Owner_NormalizedName_Type");

        Assert.True(duplicateIndex.IsUnique);
        Assert.Equal(
            "Latin1_General_100_BIN2",
            entity.FindProperty(nameof(EquipmentItem.OwnerId))!.GetCollation());
        Assert.True(entity.FindProperty(nameof(EquipmentItem.RowVersion))!.IsConcurrencyToken);
    }

    private static EquipmentDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<EquipmentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new EquipmentDbContext(options);
    }

    private static EquipmentItem Create(
        string ownerId = "owner-a",
        string name = "Saucepan",
        EquipmentType type = EquipmentType.Cookware,
        EquipmentCondition condition = EquipmentCondition.Good,
        string? brand = null) =>
        new(Guid.NewGuid(), ownerId, name, type, 1, Now, condition, brand);
}
