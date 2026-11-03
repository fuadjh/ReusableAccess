using ReusableAccess.Domain.Identity;

namespace ReusableAccess.Domain.Tests;

public sealed class IdentityRulesTests
{
    private static string Valid(string prefix)
    {
        var remainder = Enumerable.Range(0, 9)
            .Sum(i => (prefix[i] - '0') * (10 - i)) % 11;

        return prefix + (remainder < 2 ? remainder : 11 - remainder);
    }

    [Fact]
    public void National_id_preserves_zeroes_and_accepts_persian_digits()
    {
        var id = Valid("001234567");

        Assert.True(IdentityRules.IsValidNationalId(id));

        var persian = new string(id
            .Select(c => (char)('\u06f0' + c - '0')).ToArray());

        Assert.True(IdentityRules.IsValidNationalId(persian));
        Assert.Equal(id, IdentityRules.NormalizeDigits(persian));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1111111111")]
    [InlineData("0000000000")]
    [InlineData("123")]
    [InlineData("abcdefghij")]
    public void Invalid_national_ids_are_rejected(string id)
        => Assert.False(IdentityRules.IsValidNationalId(id));

    [Fact]
    public void Wrong_check_digit_is_rejected()
    {
        var id = Valid("001234567");
        var wrong = id[..9] + ((id[9] - '0' + 1) % 10);

        Assert.False(IdentityRules.IsValidNationalId(wrong));
    }

    [Fact]
    public void Registration_window_is_start_inclusive_end_exclusive()
    {
        var start = new DateTimeOffset(
            2030, 1, 1, 12, 0, 0, TimeSpan.Zero);

        var end = start.AddHours(1);

        Assert.False(IdentityRules.RegistrationIsOpen(
            false, start, end, start));

        Assert.False(IdentityRules.RegistrationIsOpen(
            true, null, end, start));

        Assert.False(IdentityRules.RegistrationIsOpen(
            true, start, end, start.AddTicks(-1)));

        Assert.True(IdentityRules.RegistrationIsOpen(
            true, start, end, start));

        Assert.False(IdentityRules.RegistrationIsOpen(
            true, start, end, end));
    }

    [Fact]
    public void Iranian_mobile_is_normalized()
    {
        Assert.Equal(
            "+989123456789",
            IdentityRules.NormalizeMobile("۰۹۱۲۳۴۵۶۷۸۹"));
    }
}