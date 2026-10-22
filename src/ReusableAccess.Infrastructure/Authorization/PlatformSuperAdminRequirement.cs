using Microsoft.AspNetCore.Authorization;

namespace ReusableAccess.Infrastructure.Authorization;

public sealed class PlatformSuperAdminRequirement
    : IAuthorizationRequirement;