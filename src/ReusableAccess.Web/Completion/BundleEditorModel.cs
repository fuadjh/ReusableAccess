using System.ComponentModel.DataAnnotations;

namespace ReusableAccess.Web.Completion;

// مدل فرم در Web می‌ماند؛ DTOها و قواعد قطعی API جایگزین نمی‌شوند.
public sealed class BundleEditorModel : IValidatableObject
{
    [Required(ErrorMessage = "نام بسته الزامی است.")]
    [StringLength(200, ErrorMessage = "نام بسته حداکثر ۲۰۰ کاراکتر است.")]
    public string Name { get; set; } = "";

    public HashSet<Guid> RoleIds { get; set; } = [];
    public string ScopeMode { get; set; } = "selected";
    public HashSet<Guid> ScopeIds { get; set; } = [];

    [Range(0, int.MaxValue, ErrorMessage = "ترتیب نمایش نمی‌تواند منفی باشد.")]
    public int SortOrder { get; set; }
    public bool IsEnabled { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (RoleIds.Count == 0 || RoleIds.Contains(Guid.Empty))
            yield return new("حداقل یک نقش معتبر انتخاب کنید.", [nameof(RoleIds)]);

        if (ScopeMode is not ("selected" or "all"))
            yield return new("حالت محدوده معتبر نیست.", [nameof(ScopeMode)]);

        if (ScopeMode == "selected" &&
            (ScopeIds.Count == 0 || ScopeIds.Contains(Guid.Empty)))
            yield return new("در حالت انتخابی، حداقل یک محدوده معتبر لازم است.",
                [nameof(ScopeIds)]);
    }
}
