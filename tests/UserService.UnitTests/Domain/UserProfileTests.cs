using UserService.Domain.Entities;
using UserService.Domain.Exceptions;

namespace UserService.UnitTests.Domain;

public class UserProfileTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_normalizes_and_assigns_server_owned_fields()
    {
        var profile = UserProfile.Create("sub-1", " Alice ", " Alice@Example.com ", " Al ", Now);

        Assert.NotEqual(Guid.Empty, profile.Id);
        Assert.Equal("sub-1", profile.Subject);
        Assert.Equal("Alice", profile.Username);
        Assert.Equal("alice", profile.NormalizedUsername);
        Assert.Equal("alice@example.com", profile.Email);
        Assert.Equal("Al", profile.DisplayName);
        Assert.Equal(Now, profile.CreatedAt);
        Assert.False(profile.IsDeleted);
    }

    [Fact]
    public void Create_with_invalid_input_throws_with_field_errors()
    {
        var ex = Assert.Throws<DomainValidationException>(() => UserProfile.Create("sub-1", "x", "bad", null, Now));

        Assert.Contains("username", ex.Errors.Keys);
        Assert.Contains("email", ex.Errors.Keys);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_requires_a_subject(string subject) =>
        Assert.Throws<DomainValidationException>(() => UserProfile.Create(subject, "alice", "a@example.com", null, Now));

    [Fact]
    public void Blank_display_name_is_stored_as_null() =>
        Assert.Null(UserProfile.Create("s", "alice", "a@example.com", "   ", Now).DisplayName);

    [Fact]
    public void Update_changes_fields_and_timestamp_but_not_identity()
    {
        var profile = UserProfile.Create("sub-1", "alice", "a@example.com", null, Now);
        var later = Now.AddHours(1);

        profile.Update("alice2", "b@example.com", "Alice", later);

        Assert.Equal("alice2", profile.Username);
        Assert.Equal("b@example.com", profile.Email);
        Assert.Equal(later, profile.UpdatedAt);
        Assert.Equal(Now, profile.CreatedAt);
        Assert.Equal("sub-1", profile.Subject);
    }

    [Fact]
    public void Delete_is_a_soft_delete_and_idempotent()
    {
        var profile = UserProfile.Create("sub-1", "alice", "a@example.com", null, Now);

        profile.Delete(Now.AddMinutes(1));
        profile.Delete(Now.AddMinutes(5));

        Assert.True(profile.IsDeleted);
        Assert.Equal(Now.AddMinutes(1), profile.DeletedAt);
    }

    [Fact]
    public void Deleted_profile_cannot_be_updated()
    {
        var profile = UserProfile.Create("sub-1", "alice", "a@example.com", null, Now);
        profile.Delete(Now);

        Assert.Throws<UserNotFoundException>(() => profile.Update("alice", "a@example.com", null, Now));
    }
}
