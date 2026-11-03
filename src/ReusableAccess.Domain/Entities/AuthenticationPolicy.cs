using ReusableAccess.Domain.Identity;

namespace ReusableAccess.Domain.Entities;

public sealed class AuthenticationPolicy
{
    public int Id { get; set; } = 1;
    public bool PublicRegistrationEnabled { get; set; }
    public DateTimeOffset? RegistrationStartsUtc { get; set; }
    public DateTimeOffset? RegistrationEndsUtc { get; set; }

    public LoginMode LoginMode { get; set; } = LoginMode.Password;

    public bool RecoveryEnabled { get; set; }
    public DeliveryChannel RecoveryChannel { get; set; }
        = DeliveryChannel.Email;

    public int Version { get; set; }

    public bool IsRegistrationOpen(DateTimeOffset now)
    {
        return IdentityRules.RegistrationIsOpen(
            PublicRegistrationEnabled,
            RegistrationStartsUtc,
            RegistrationEndsUtc,
            now);
    }
}

public sealed class AccountChallenge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Purpose { get; set; } = "";
    public DeliveryChannel Channel { get; set; }

    // OTP خام در دیتابیس قرار نمی‌گیرد.
    public string ProtectedCode { get; set; } = "";

    public int UserSessionVersion { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset ExpiresUtc { get; set; }
    public int Attempts { get; set; }
    public bool Consumed { get; set; }
    public bool RememberMe { get; set; }
    public int Version { get; set; }
}

public sealed class SecurityAuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ActorId { get; set; }
    public DateTimeOffset OccurredUtc { get; set; }
    public string Action { get; set; } = "";
    public string Entity { get; set; } = "";
    public string EntityId { get; set; } = "";

    // نام فیلدها ثبت می‌شود، نه مقدار حساس آن‌ها.
    public string ChangedFields { get; set; } = "";
}