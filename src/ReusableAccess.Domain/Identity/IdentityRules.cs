namespace ReusableAccess.Domain.Identity;

public enum LoginMode
{
    Password = 0,
    PasswordAndEmail = 1,
    PasswordAndSms = 2
}

public enum DeliveryChannel
{
    Email = 0,
    Sms = 1
}

public static class IdentityRules
{
    public static string NormalizeDigits(string? value)
    {
        return new string((value ?? "").Trim().Select(c =>
            c is >= '\u06f0' and <= '\u06f9'
                ? (char)('0' + c - '\u06f0')
                : c is >= '\u0660' and <= '\u0669'
                    ? (char)('0' + c - '\u0660')
                    : c).ToArray());
    }

    public static bool IsValidNationalId(string? value)
    {
        var code = NormalizeDigits(value);

        if (code.Length != 10 ||
            code.Any(c => c < '0' || c > '9') ||
            code.Distinct().Count() == 1)
        {
            return false;
        }

        var sum = Enumerable.Range(0, 9)
            .Sum(i => (code[i] - '0') * (10 - i));

        var remainder = sum % 11;
        var expected = remainder < 2 ? remainder : 11 - remainder;

        return code[9] - '0' == expected;
    }

    public static string NormalizeMobile(string? value)
    {
        var number = NormalizeDigits(value);

        if (number.StartsWith("09") && number.Length == 11)
            return "+98" + number[1..];

        if (number.StartsWith("0098"))
            return "+" + number[2..];

        return number;
    }

    public static bool RegistrationIsOpen(
        bool enabled,
        DateTimeOffset? start,
        DateTimeOffset? end,
        DateTimeOffset now)
    {
        // بازه: شروع شامل و پایان خارج از بازه است.
        return enabled &&
               start.HasValue &&
               end.HasValue &&
               start < end &&
               now >= start &&
               now < end;
    }
}