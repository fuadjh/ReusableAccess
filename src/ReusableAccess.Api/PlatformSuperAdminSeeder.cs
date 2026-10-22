using Microsoft.AspNetCore.Identity;
using ReusableAccess.Infrastructure.Identity;

namespace ReusableAccess.Api;

public static class PlatformSuperAdminSeeder
{
    public static async Task SeedAsync(
        IServiceProvider services,
        IConfiguration configuration)
    {
        var userName = configuration["PlatformSuperAdmin:UserName"];

        // اگر تنظیم نشده باشد، هیچ کاربری خودکار مدیر نمی‌شود.
        if (string.IsNullOrWhiteSpace(userName))
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByNameAsync(userName);

        if (user is null)
        {
            throw new InvalidOperationException(
                "Configured platform superadmin user was not found.");
        }

        if (user.IsPlatformSuperAdmin)
        {
            return;
        }

        user.IsPlatformSuperAdmin = true;

        var result = await userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Could not configure the platform superadmin user.");
        }
    }
}