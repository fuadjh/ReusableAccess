using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReusableAccess.Application.Completion;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Domain.Enums;
using ReusableAccess.Domain.Identity;
using ReusableAccess.Infrastructure.Identity;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure;

public static class CompletionBootstrap
{
    public static async Task BootstrapCompletionAdminAsync(
        this IServiceProvider services,
        IConfiguration config,
        CancellationToken ct = default)
    {
        if (!config.GetValue("BootstrapAdmin:Enabled", false))
            return;

        await using var scope = services.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var manager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var input = new UserProfileInput
        {
            FirstName = config["BootstrapAdmin:FirstName"] ?? "",
            LastName = config["BootstrapAdmin:LastName"] ?? "",
            OrganizationalCode = config["BootstrapAdmin:OrganizationalCode"] ?? "",
            NationalId = config["BootstrapAdmin:NationalId"] ?? "",
            Email = config["BootstrapAdmin:Email"] ?? "",
            Mobile = config["BootstrapAdmin:Mobile"] ?? ""
        };

        CompletionValidation.Profile(input);

        var national = IdentityRules.NormalizeDigits(input.NationalId);
        var legacyName = config["PlatformSuperAdmin:UserName"];

        ApplicationUser? user = null;

        if (!string.IsNullOrWhiteSpace(legacyName))
            user = await manager.FindByNameAsync(legacyName);

        user ??= await db.Users.SingleOrDefaultAsync(
            x => x.IsPlatformSuperAdmin && x.NationalId == national, ct);

        if (await db.Users.AnyAsync(
                x => (user == null || x.Id != user.Id) &&
                     (x.NationalId == national || x.UserName == national), ct))
        {
            throw new InvalidOperationException(
                "Bootstrap NationalId conflicts with another existing account.");
        }

        var created = user is null;
        user ??= new ApplicationUser
        {
            Id = Guid.NewGuid(),
            LockoutEnabled = true,
            MustChangePassword = true
        };

        var oldName = user.UserName;
        var mobile = IdentityRules.NormalizeMobile(input.Mobile);

        var changed =
            created || user.FirstName != input.FirstName.Trim() ||
            user.LastName != input.LastName.Trim() ||
            user.UserName != national || user.NationalId != national ||
            user.OrganizationalCode != input.OrganizationalCode.Trim() ||
            user.Email != input.Email.Trim() ||
            user.PhoneNumber != mobile ||
            !user.IsActive || !user.IsPlatformSuperAdmin;

        if (changed)
        {
            user.FirstName = input.FirstName.Trim();
            user.LastName = input.LastName.Trim();
            user.DisplayName = $"{user.FirstName} {user.LastName}";
            user.OrganizationalCode = input.OrganizationalCode.Trim();
            user.UserName = national;
            user.NationalId = national;

            if (user.Email != input.Email.Trim())
            {
                user.Email = input.Email.Trim();
                user.EmailConfirmed = false;
            }

            if (user.PhoneNumber != mobile)
            {
                user.PhoneNumber = mobile;
                user.PhoneNumberConfirmed = false;
            }

            user.IsActive = true;
            user.IsPlatformSuperAdmin = true;
            user.SessionVersion++;

            // رمز حساب موجود هرگز در startup بازنشانی نمی‌شود.
            var result = created
                ? await manager.CreateAsync(
                    user, config["BootstrapAdmin:Password"] ?? "")
                : await manager.UpdateAsync(user);

            if (!result.Succeeded)
                throw new InvalidOperationException(
                    "Admin bootstrap failed: " +
                    string.Join(";", result.Errors.Select(x => x.Code)));
        }

        config["PlatformSuperAdmin:UserName"] = national;

        if (!string.IsNullOrWhiteSpace(oldName) &&
            config["DevelopmentUser:UserName"] == oldName)
        {
            config["DevelopmentUser:UserName"] = national;
        }
    }

    public static async Task SeedCompletionDemoAsync(
        this IServiceProvider services,
        IConfiguration config,
        IHostEnvironment environment,
        CancellationToken ct = default)
    {
        if (!environment.IsDevelopment() ||
            !config.GetValue("Demo:Enabled", false))
            return;

        var password = config["Demo:Password"]
            ?? throw new InvalidOperationException(
                "Demo:Password must be configured in User Secrets.");

        await using var scope = services.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var manager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        async Task<AccessScope> GetScope(string code)
        {
            var s = await db.Set<AccessScope>()
                .SingleOrDefaultAsync(x => x.Code == code, ct);

            if (s is not null)
                return s;

            s = new AccessScope(code,
                code == "DEMO_A" ? "واحد آزمایشی الف" : "واحد آزمایشی ب");

            db.Add(s);
            await db.SaveChangesAsync(ct);
            return s;
        }

        var a = await GetScope("DEMO_A");
        var b = await GetScope("DEMO_B");

        var definitions = new[]
        {
            (Base: "001234567", Name: "مشاهده‌گر", Role: "DEMO_VIEWER",
                Scope: a, Keys: new[] { "workitems.read", "diagnostics.read" }),

            (Base: "001234568", Name: "ویرایشگر", Role: "DEMO_OPERATOR",
                Scope: a, Keys: new[] {
                    "workitems.read", "workitems.create",
                    "workitems.update", "workitems.delete", "diagnostics.read" }),

            (Base: "001234569", Name: "گزارش‌گیر", Role: "DEMO_REPORTER",
                Scope: b, Keys: new[] { "workitems.read", "reports.read" }),

            (Base: "001234570", Name: "بدون دسترسی", Role: "DEMO_NONE",
                Scope: a, Keys: Array.Empty<string>())
        };

        foreach (var d in definitions)
        {
            var remainder = Enumerable.Range(0, 9)
                .Sum(i => (d.Base[i] - '0') * (10 - i)) % 11;

            var digit = remainder < 2 ? remainder : 11 - remainder;
            var national = d.Base + digit.ToString(CultureInfo.InvariantCulture);

            var user = await manager.FindByNameAsync(national);

            if (user is null)
            {
                user = new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    UserName = national,
                    NationalId = national,
                    FirstName = "کاربر",
                    LastName = d.Name,
                    DisplayName = "کاربر " + d.Name,
                    OrganizationalCode = d.Role,
                    Email = d.Role.ToLowerInvariant() + "@example.invalid",
                    PhoneNumber = "+999" + d.Base,
                    IsActive = true,
                    LockoutEnabled = true
                };

                var result = await manager.CreateAsync(user, password);

                if (!result.Succeeded)
                    throw new InvalidOperationException(
                        "Demo user creation failed: " +
                        string.Join(";", result.Errors.Select(x => x.Code)));
            }

            if (d.Keys.Length == 0)
                continue;

            var role = await db.Roles.Include(x => x.Permissions)
                .SingleOrDefaultAsync(x => x.Code == d.Role, ct);

            if (role is null)
            {
                role = new Role(d.Role, d.Name);
                db.Add(role);
                await db.SaveChangesAsync(ct);
            }

            foreach (var key in d.Keys)
            {
                role.AddPermission(await db.Set<Permission>()
                    .SingleAsync(x => x.Key == key, ct));
            }

            if (!await db.Set<UserAccessBundle>()
                    .AnyAsync(x => x.UserId == user.Id, ct))
            {
                db.Add(new UserAccessBundle(
                    user.Id, "پروفایل آزمایشی", [role.Id],
                    ScopeSelectionMode.Selected, [d.Scope.Id]));
            }

            if (!await db.Set<WorkItem>().AnyAsync(
                    x => x.ScopeId == d.Scope.Id &&
                         x.Title == "مورد کاری آزمایشی", ct))
            {
                db.Add(new WorkItem(
                    d.Scope.Id, "مورد کاری آزمایشی", "Open"));
            }

            await db.SaveChangesAsync(ct);
        }
    }
}