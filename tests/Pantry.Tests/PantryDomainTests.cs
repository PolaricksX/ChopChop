using Pantry.Domain.Entities;
using Pantry.Domain.Enums;
using Pantry.Domain.Services;

namespace Pantry.Tests;

public class PantryDomainTests
{
    [Fact]
    public void Rejects_non_positive_quantity() => Assert.Throws<ArgumentOutOfRangeException>(() => new PantryItem(Guid.NewGuid(), "user", "Milk", PantryType.Grains, 0, Unit.Liter, null));
    [Fact]
    public void Detects_expiring_items()
    {
        var item = new PantryItem(Guid.NewGuid(), "user", "Milk", PantryType.Grains, 1, Unit.Liter, expiresAt: DateTimeOffset.UtcNow.AddDays(2));
        Assert.True(PantryRules.IsExpiring(item, DateTimeOffset.UtcNow, TimeSpan.FromDays(3)));
    }

    [Fact]
    public void Normalizes_name_but_preserves_owner_exactly()
    {
        var item = new PantryItem(Guid.NewGuid(), " user 1 ", "  Red   Beans ", PantryType.Grains, 1, Unit.Kilogram);

        Assert.Equal(" user 1 ", item.OwnerId);
        Assert.Equal("Red Beans", item.Name);
    }

    [Fact]
    public void Consuming_item_sets_quantity_to_zero_and_cannot_be_repeated()
    {
        var item = new PantryItem(Guid.NewGuid(), "user", "Milk", "Dairy", 1, Unit.Liter);

        item.Consume();

        Assert.Equal(0, item.Quantity);
        Assert.Equal(Availability.Consumed, item.Availability);
        Assert.Throws<InvalidOperationException>(item.Consume);
    }

    [Fact]
    public void Unavailable_item_has_distinct_state_and_preserves_quantity()
    {
        var item = new PantryItem(Guid.NewGuid(), "user", "Milk", PantryType.Grains, 2, Unit.Liter);

        item.MarkUnavailable();

        Assert.Equal(Availability.Unavailable, item.Availability);
        Assert.Equal(2, item.Quantity);
        Assert.Throws<InvalidOperationException>(item.MarkUnavailable);
    }

    [Fact]
    public void Custom_items_require_and_retain_their_category()
    {
        Assert.Throws<ArgumentException>(() =>
            new PantryItem(Guid.NewGuid(), "user", "Thing", PantryType.Custom, 1, Unit.Count));

        var item = new PantryItem(Guid.NewGuid(), "user", "Thing", "  Baking  ", 1, Unit.Count);
        Assert.Equal("Baking", item.CustomCategory);
    }

    [Fact]
    public void Update_replaces_all_editable_fields()
    {
        var item = new PantryItem(Guid.NewGuid(), "user", "Thing", "Baking", 1, Unit.Count);

        item.Update("New Thing", PantryType.Custom, "Canned", "A note", 3, Unit.Kilogram,
            DateTimeOffset.UtcNow.AddDays(4));

        Assert.Equal("New Thing", item.Name);
        Assert.Equal("Canned", item.CustomCategory);
        Assert.Equal("A note", item.Notes);
        Assert.Equal(3, item.Quantity);
        Assert.Equal(Unit.Kilogram, item.Unit);
        Assert.NotNull(item.ExpiresAt);
    }

    [Fact]
    public void Duplicate_identity_is_scoped_to_owner_and_type()
    {
        var first = new PantryItem(Guid.NewGuid(), "user", "  Rice ", PantryType.Grains, 1, Unit.Kilogram);
        var duplicate = new PantryItem(Guid.NewGuid(), "user", "Rice", PantryType.Grains, 2, Unit.Kilogram);
        var differentOwner = new PantryItem(Guid.NewGuid(), "other", "Rice", PantryType.Grains, 1, Unit.Kilogram);

        Assert.True(first.IsDuplicateOf(duplicate));
        Assert.False(first.IsDuplicateOf(differentOwner));
        Assert.False(first.IsDuplicateOf(new PantryItem(Guid.NewGuid(), "USER", "Rice", PantryType.Grains, 1, Unit.Count)));
    }
}
