using Microsoft.AspNetCore.Authorization;

namespace ReusableAccess.Infrastructure.Authorization;

public sealed record PermissionRequirement(string PermissionKey)
    : IAuthorizationRequirement;