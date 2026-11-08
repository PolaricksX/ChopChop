using Equipment.Api.Contracts;
using Equipment.Api.Services;
using Equipment.Domain.Enums;

namespace Equipment.UnitTests.Api;

public sealed class EquipmentRequestValidatorTests
{
    [Fact]
    public void Create_requires_a_complete_body_and_an_idempotency_key()
    {
        var errors = EquipmentRequestValidator.ValidateCreate(null, null);

        Assert.Contains("request", errors.Keys);
        Assert.Contains("Idempotency-Key", errors.Keys);
    }

    [Fact]
    public void Create_rejects_invalid_fields_and_oversized_optional_values()
    {
        var errors = EquipmentRequestValidator.ValidateCreate(
            new EquipmentUpsertRequest(
                " ",
                (EquipmentType)999,
                0,
                null,
                new string('b', 101),
                null,
                null,
                null),
            "valid-key");

        Assert.Contains("name", errors.Keys);
        Assert.Contains("type", errors.Keys);
        Assert.Contains("quantity", errors.Keys);
        Assert.Contains("condition", errors.Keys);
        Assert.Contains("brand", errors.Keys);
    }

    [Fact]
    public void List_uses_the_documented_default_and_maximum_page_sizes()
    {
        Assert.Equal(20, EquipmentRequestValidator.DefaultPageSize);
        Assert.Equal(100, EquipmentRequestValidator.MaximumPageSize);
        Assert.Empty(EquipmentRequestValidator.ValidateList(1, 20, null, null, null, null));
        Assert.Contains(
            "pageSize",
            EquipmentRequestValidator.ValidateList(1, 101, null, null, null, null).Keys);
    }

    [Fact]
    public void Version_token_requires_a_strong_nonempty_base64_etag()
    {
        const string version = "AQIDBAUGBwg=";
        Assert.True(VersionToken.TryDecode($"\"{version}\"", out var decoded));
        Assert.Equal(Convert.FromBase64String(version), decoded);
        Assert.False(VersionToken.TryDecode($"W/\"{version}\"", out _));
        Assert.False(VersionToken.TryDecode("\"\"", out _));
        Assert.False(VersionToken.TryDecode("\"AQID\"", out _));
        Assert.False(VersionToken.TryDecode("not-base64", out _));
    }
}
