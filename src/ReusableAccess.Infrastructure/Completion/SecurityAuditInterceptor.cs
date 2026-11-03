using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Identity;

namespace ReusableAccess.Infrastructure.Completion;

public sealed class SecurityAuditInterceptor(
    IHttpContextAccessor accessor,
    TimeProvider time) : SaveChangesInterceptor
{
    private static readonly HashSet<string> Names =
    [
        nameof(ApplicationUser), nameof(Role), nameof(RolePermission),
        nameof(AccessScope), nameof(UserRoleAssignment),
        nameof(UserAccessBundle), nameof(UserAccessBundleRole),
        nameof(UserAccessBundleScope), nameof(WorkItem),
        nameof(AuthenticationPolicy)
    ];

    private static void Add(
        DbContext? db, ClaimsPrincipal? principal, DateTimeOffset now)
    {
        if (db is null)
            return;

        Guid? actor = Guid.TryParse(
            principal?.FindFirst("sub")?.Value ??
            principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            out var id) ? id : null;

        var entries = db.ChangeTracker.Entries()
            .Where(e => Names.Contains(e.Metadata.ClrType.Name) &&
                e.State is EntityState.Added or
                    EntityState.Modified or EntityState.Deleted)
            .ToArray();

        foreach (var entry in entries)
        {
            // فقط نام فیلدها؛ مقدار رمز، توکن یا مشخصات تماس ثبت نمی‌شود.
            var fields = entry.Properties
                .Where(p => p.IsModified &&
                    p.Metadata.Name is not
                        ("PasswordHash" or "SecurityStamp" or "ConcurrencyStamp"))
                .Select(p => p.Metadata.Name)
                .OrderBy(x => x);

            var keys = entry.Properties
                .Where(p => p.Metadata.IsPrimaryKey())
                .Select(p => p.CurrentValue);

            db.Set<SecurityAuditEntry>().Add(new()
            {
                ActorId = actor,
                OccurredUtc = now,
                Action = entry.State.ToString(),
                Entity = entry.Metadata.ClrType.Name,
                EntityId = string.Join(",", keys),
                ChangedFields = string.Join(",", fields)
            });
        }
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData e, InterceptionResult<int> result)
    {
        Add(e.Context, accessor.HttpContext?.User, time.GetUtcNow());
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData e,
        InterceptionResult<int> result,
        CancellationToken ct = default)
    {
        Add(e.Context, accessor.HttpContext?.User, time.GetUtcNow());
        return ValueTask.FromResult(result);
    }
}