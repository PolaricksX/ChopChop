using UserService.Api.Contracts;

namespace UserService.UnitTests.Api;

public class UserRequestValidatorTests
{
    [Fact]
    public void Create_requires_body_and_idempotency_key()
    {
        var errors = UserRequestValidator.Validate((CreateUserRequest?)null, null);

        Assert.Contains("body", errors.Keys);
        Assert.Contains("Idempotency-Key", errors.Keys);
    }

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("tab\there")]
    public void Create_rejects_bad_idempotency_keys(string key) =>
        Assert.Contains("Idempotency-Key", UserRequestValidator.Validate(new CreateUserRequest("alice", "a@example.com", null), key).Keys);

    [Fact]
    public void Create_rejects_oversized_idempotency_key() =>
        Assert.Contains("Idempotency-Key", UserRequestValidator.Validate(new CreateUserRequest("alice", "a@example.com", null), new string('k', 129)).Keys);

    [Fact]
    public void Create_accepts_valid_input() =>
        Assert.Empty(UserRequestValidator.Validate(new CreateUserRequest("alice", "a@example.com", null), "abc-123"));

    [Fact]
    public void Update_requires_a_well_formed_version()
    {
        Assert.Contains("version", UserRequestValidator.Validate(new UpdateUserRequest("alice", "a@example.com", null, null)).Keys);
        Assert.Contains("version", UserRequestValidator.Validate(new UpdateUserRequest("alice", "a@example.com", null, "not-base64!")).Keys);
        Assert.Contains("version", UserRequestValidator.Validate(new UpdateUserRequest("alice", "a@example.com", null, "AAAA")).Keys);
        Assert.Empty(UserRequestValidator.Validate(new UpdateUserRequest("alice", "a@example.com", null, "AAAAAAAAAAA=")));
    }

    [Fact]
    public void Update_requires_body() =>
        Assert.Contains("body", UserRequestValidator.Validate((UpdateUserRequest?)null).Keys);

    [Fact]
    public void Version_token_round_trips()
    {
        byte[] row = [1, 2, 3, 4, 5, 6, 7, 8];

        Assert.True(VersionToken.TryDecode(VersionToken.Encode(row), out var decoded));
        Assert.Equal(row, decoded);
    }
}
