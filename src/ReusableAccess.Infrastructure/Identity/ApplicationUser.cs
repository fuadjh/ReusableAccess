using Microsoft.AspNetCore.Identity;

namespace ReusableAccess.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string NationalId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
}