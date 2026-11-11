using System.ComponentModel.DataAnnotations;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Identity;

namespace ReusableAccess.Web.Ui;

public sealed class ManagedUserEditorModel
    : UserProfileInput, IValidatableObject
{
    public bool IsNew { get; init; } = true;
    public bool NationalIdReadOnly { get; init; }
    public string Password { get; set; } = "";

    public static ManagedUserEditorModel From(UserRow? user)
        => new()
        {
            IsNew = user is null,
            NationalIdReadOnly =
                user is not null &&
                IdentityRules.IsValidNationalId(user.NationalId),
            FirstName = user?.FirstName ?? "",
            LastName = user?.LastName ?? "",
            OrganizationalCode = user?.OrganizationalCode ?? "",
            NationalId = user?.NationalId ?? "",
            Email = user?.Email ?? "",
            Mobile = user?.Mobile ?? ""
        };

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (IsNew &&
            (string.IsNullOrWhiteSpace(Password) ||
             Password.Length is < 12 or > 128))
        {
            yield return new ValidationResult(
                "گذرواژهٔ موقت باید بین ۱۲ تا ۱۲۸ کاراکتر باشد.",
                [nameof(Password)]);
        }
    }

    public CreateUserInput ToCreate() => new()
    {
        FirstName = FirstName,
        LastName = LastName,
        OrganizationalCode = OrganizationalCode,
        NationalId = NationalId,
        Email = Email,
        Mobile = Mobile,
        Password = Password
    };

    public UpdateUserInput ToUpdate(string concurrencyStamp) => new()
    {
        FirstName = FirstName,
        LastName = LastName,
        OrganizationalCode = OrganizationalCode,
        NationalId = NationalId,
        Email = Email,
        Mobile = Mobile,
        ConcurrencyStamp = concurrencyStamp
    };
}

public sealed class ManagedPasswordModel
{
    [Required(ErrorMessage = "گذرواژه الزامی است.")]
    [StringLength(
        128,
        MinimumLength = 12,
        ErrorMessage = "گذرواژه باید بین ۱۲ تا ۱۲۸ کاراکتر باشد.")]
    public string Password { get; set; } = "";

    [Compare(
        nameof(Password),
        ErrorMessage = "تکرار گذرواژه مطابقت ندارد.")]
    public string Confirmation { get; set; } = "";
}