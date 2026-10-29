using UserService.Domain.Services;

namespace UserService.UnitTests.Domain;

public class UserProfileRulesTests
{
    [Theory]
    [InlineData("alice", "alice@example.com")]
    [InlineData("A.li-ce_9", "first.last+tag@sub.example.co")]
    [InlineData("abc", "a@b.io")]
    public void Valid_input_has_no_errors(string username, string email) =>
        Assert.Empty(UserProfileRules.Validate(username, email, "Alice"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("ab")]
    [InlineData("_alice")]
    [InlineData("has space")]
    [InlineData("semi;colon")]
    [InlineData("<script>")]
    public void Invalid_username_is_rejected(string? username) =>
        Assert.Contains("username", UserProfileRules.Validate(username, "a@example.com", null).Keys);

    [Fact]
    public void Username_over_30_characters_is_rejected() =>
        Assert.Contains("username", UserProfileRules.Validate(new string('a', 31), "a@example.com", null).Keys);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("plainaddress")]
    [InlineData("a@localhost")]
    [InlineData("a b@example.com")]
    [InlineData("Alice <a@example.com>")]
    [InlineData("a@example.")]
    public void Invalid_email_is_rejected(string? email) =>
        Assert.Contains("email", UserProfileRules.Validate("alice", email, null).Keys);

    [Fact]
    public void Email_over_254_characters_is_rejected() =>
        Assert.Contains("email", UserProfileRules.Validate("alice", new string('a', 250) + "@x.io", null).Keys);

    [Fact]
    public void Display_name_limits_are_enforced()
    {
        Assert.Contains("displayName", UserProfileRules.Validate("alice", "a@example.com", new string('x', 101)).Keys);
        Assert.Contains("displayName", UserProfileRules.Validate("alice", "a@example.com", "bad\nname").Keys);
    }

    [Fact]
    public void Normalization_is_case_insensitive_and_trimmed()
    {
        Assert.Equal("a@example.com", UserProfileRules.NormalizeEmail("  A@Example.COM "));
        Assert.Equal("alice", UserProfileRules.NormalizeUsername(" Alice "));
    }
}
