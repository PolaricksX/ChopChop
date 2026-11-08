using Equipment.Domain.Entities;
using Equipment.Domain.Enums;
using Equipment.Domain.Exceptions;

namespace Equipment.UnitTests.Domain;

public sealed class EquipmentItemTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_accepts_the_supported_equipment_taxonomy()
    {
        var types = Enum.GetValues<EquipmentType>();

        Assert.Contains(EquipmentType.Cookware, types);
        Assert.Contains(EquipmentType.Bakeware, types);
        Assert.Contains(EquipmentType.Knives, types);
        Assert.Contains(EquipmentType.Utensils, types);
        Assert.Contains(EquipmentType.Appliances, types);
        Assert.Contains(EquipmentType.PrepTools, types);
        Assert.Contains(EquipmentType.Measuring, types);
        Assert.Contains(EquipmentType.Serving, types);
        Assert.Contains(EquipmentType.Storage, types);
        Assert.Contains(EquipmentType.Barware, types);
        Assert.Contains(EquipmentType.Other, types);
    }

    [Fact]
    public void Create_normalizes_name_and_optional_details()
    {
        var item = Create(
            name: "  Cast   Iron   Pan ",
            brand: "  Acme  ",
            size: "  12   inch ",
            notes: "  Seasoned   twice ",
            location: "  Bottom   drawer ");

        Assert.Equal("Cast Iron Pan", item.Name);
        Assert.Equal("CAST IRON PAN", item.NormalizedName);
        Assert.Equal("Acme", item.Brand);
        Assert.Equal("12 inch", item.Size);
        Assert.Equal("Seasoned twice", item.Notes);
        Assert.Equal("Bottom drawer", item.Location);
    }

    [Fact]
    public void Create_rejects_empty_owner_id_and_equipment_id()
    {
        Assert.Throws<EquipmentDomainException>(() =>
            new EquipmentItem(Guid.Empty, "owner", "Pan", EquipmentType.Cookware, 1, Now));
        Assert.Throws<EquipmentDomainException>(() =>
            new EquipmentItem(Guid.NewGuid(), " ", "Pan", EquipmentType.Cookware, 1, Now));
        Assert.Throws<EquipmentDomainException>(() =>
            Create(ownerId: new string('x', EquipmentItem.MaxOwnerIdLength + 1)));
    }

    [Fact]
    public void Create_rejects_invalid_type_and_condition_values()
    {
        Assert.Throws<EquipmentDomainException>(() =>
            Create(type: (EquipmentType)999));
        Assert.Throws<EquipmentDomainException>(() =>
            Create(condition: (EquipmentCondition)999));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public void Quantity_must_be_between_one_and_one_thousand(int quantity)
    {
        Assert.Throws<EquipmentDomainException>(() => Create(quantity: quantity));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1000)]
    public void Quantity_accepts_inclusive_bounds(int quantity)
    {
        Assert.Equal(quantity, Create(quantity: quantity).Quantity);
    }

    [Fact]
    public void Create_rejects_required_or_oversized_name()
    {
        Assert.Throws<EquipmentDomainException>(() => Create(name: "  "));
        Assert.Throws<EquipmentDomainException>(() =>
            Create(name: new string('x', EquipmentItem.MaxNameLength + 1)));
    }

    [Theory]
    [InlineData("brand", 101)]
    [InlineData("size", 101)]
    [InlineData("notes", 501)]
    [InlineData("location", 121)]
    public void Optional_details_are_bounded(string field, int length)
    {
        var value = new string('x', length);

        Assert.Throws<EquipmentDomainException>(() => field switch
        {
            "brand" => Create(brand: value),
            "size" => Create(size: value),
            "notes" => Create(notes: value),
            "location" => Create(location: value),
            _ => throw new InvalidOperationException(),
        });
    }

    [Fact]
    public void Blank_optional_details_are_normalized_to_null()
    {
        var item = Create(brand: " ", size: "\t", notes: "\n", location: " ");

        Assert.Null(item.Brand);
        Assert.Null(item.Size);
        Assert.Null(item.Notes);
        Assert.Null(item.Location);
    }

    [Fact]
    public void Duplicate_identity_uses_owner_normalized_name_and_type_only()
    {
        var first = Create(name: "  chef   knife ");
        var sameIdentity = Create(name: "Chef Knife", id: Guid.NewGuid(), quantity: 3, brand: "Other");

        Assert.True(first.HasSameIdentity(sameIdentity));
        Assert.False(first.HasSameIdentity(Create(ownerId: "another-user", name: "Chef Knife")));
        Assert.False(first.HasSameIdentity(Create(name: "Chef Knife", type: EquipmentType.Utensils)));
        Assert.False(first.HasSameIdentity(Create(name: "Chef Knife, 8 inch")));
    }

    [Fact]
    public void Availability_can_be_changed_in_either_direction_and_is_idempotent()
    {
        var item = Create();
        var updatedAt = Now.AddMinutes(1);

        item.SetAvailability(EquipmentAvailability.Unavailable, updatedAt);
        Assert.Equal(EquipmentAvailability.Unavailable, item.Availability);
        Assert.Equal(updatedAt, item.UpdatedAtUtc);

        item.SetAvailability(EquipmentAvailability.Unavailable, updatedAt.AddMinutes(1));
        Assert.Equal(updatedAt, item.UpdatedAtUtc);

        item.SetAvailability(EquipmentAvailability.Available, updatedAt.AddMinutes(2));
        Assert.Equal(EquipmentAvailability.Available, item.Availability);
    }

    [Fact]
    public void Availability_rejects_undefined_values()
    {
        Assert.Throws<EquipmentDomainException>(() =>
            Create().SetAvailability((EquipmentAvailability)999, Now));
    }

    [Fact]
    public void Condition_accepts_every_defined_value_and_rejects_undefined_values()
    {
        var item = Create();
        foreach (var condition in Enum.GetValues<EquipmentCondition>())
        {
            item.SetCondition(condition, Now);
            Assert.Equal(condition, item.Condition);
        }

        Assert.Throws<EquipmentDomainException>(() =>
            item.SetCondition((EquipmentCondition)999, Now));
    }

    [Fact]
    public void Update_is_atomic_when_a_value_is_invalid()
    {
        var item = Create(name: "Pan", quantity: 2);

        Assert.Throws<EquipmentDomainException>(() =>
            item.Update(
                "Updated pan",
                EquipmentType.Bakeware,
                0,
                EquipmentCondition.Fair,
                "Brand",
                "Large",
                "Notes",
                "Cabinet",
                Now.AddMinutes(1)));

        Assert.Equal("Pan", item.Name);
        Assert.Equal(EquipmentType.Cookware, item.Type);
        Assert.Equal(2, item.Quantity);
        Assert.Null(item.Brand);
        Assert.Equal(Now, item.UpdatedAtUtc);
    }

    [Fact]
    public void Update_normalizes_details_and_uses_utc_timestamps()
    {
        var item = Create();
        var localTime = new DateTimeOffset(2026, 11, 4, 7, 0, 0, TimeSpan.FromHours(-5));

        var changed = item.Update(
            "  baking   tray ",
            EquipmentType.Bakeware,
            2,
            EquipmentCondition.New,
            "  Acme ",
            "  9 x 13  ",
            null,
            "  upper   shelf ",
            localTime);

        Assert.True(changed);
        Assert.Equal("baking tray", item.Name);
        Assert.Equal("BAKING TRAY", item.NormalizedName);
        Assert.Equal(EquipmentType.Bakeware, item.Type);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(EquipmentCondition.New, item.Condition);
        Assert.Equal("Acme", item.Brand);
        Assert.Equal("9 x 13", item.Size);
        Assert.Null(item.Notes);
        Assert.Equal("upper shelf", item.Location);
        Assert.Equal(localTime.ToUniversalTime(), item.UpdatedAtUtc);
    }

    [Fact]
    public void Update_reports_changes_even_when_the_clock_does_not_advance()
    {
        var item = Create();

        var changed = item.Update(
            item.Name,
            item.Type,
            item.Quantity + 1,
            item.Condition,
            item.Brand,
            item.Size,
            item.Notes,
            item.Location,
            Now);
        var unchanged = item.Update(
            item.Name,
            item.Type,
            item.Quantity,
            item.Condition,
            item.Brand,
            item.Size,
            item.Notes,
            item.Location,
            Now.AddMinutes(1));

        Assert.True(changed);
        Assert.False(unchanged);
        Assert.Equal(Now, item.UpdatedAtUtc);
    }

    private static EquipmentItem Create(
        Guid? id = null,
        string ownerId = "owner-1",
        string name = "Cast iron pan",
        EquipmentType type = EquipmentType.Cookware,
        int quantity = 1,
        EquipmentCondition condition = EquipmentCondition.Good,
        string? brand = null,
        string? size = null,
        string? notes = null,
        string? location = null) =>
        new(id ?? Guid.NewGuid(), ownerId, name, type, quantity, Now, condition, brand, size, notes, location);
}
