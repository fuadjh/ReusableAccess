using System.ComponentModel.DataAnnotations;

namespace ReusableAccess.Contracts;

public sealed class UpdateOwnNamesInput
{
    [Required(ErrorMessage = "نام الزامی است.")]
    [StringLength(100)]
    public string FirstName { get; set; } = "";

    [Required(ErrorMessage = "نام خانوادگی الزامی است.")]
    [StringLength(100)]
    public string LastName { get; set; } = "";

    [Required(ErrorMessage = "اطلاعات حساب باید دوباره دریافت شود.")]
    [StringLength(256)]
    public string ConcurrencyStamp { get; set; } = "";
}