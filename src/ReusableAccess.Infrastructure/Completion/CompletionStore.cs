using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Completion;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Domain.Enums;
using ReusableAccess.Domain.Identity;
using ReusableAccess.Infrastructure.Identity;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure.Completion;

public sealed class CompletionStore(
    ApplicationDbContext db,
    UserManager<ApplicationUser> users,
    IAccountMessageSender sender)
    : ICompletionStore, IAccountSessionValidator
{
    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new CompletionException(
                400, string.Join(" ", result.Errors.Select(x => x.Description)));
    }

   
    private async Task<ApplicationUser> User(Guid id, CancellationToken ct)
        => await db.Users.SingleOrDefaultAsync(x => x.Id == id, ct)
           ?? throw new CompletionException(404, "کاربر پیدا نشد.");

    public Task<bool> IsCurrentAsync(
        Guid id, int version, CancellationToken ct)
        => db.Users.AsNoTracking().AnyAsync(
            x => x.Id == id && x.IsActive &&
                 x.SessionVersion == version, ct);

  
    
       

   

   

    public async Task<bool> ChangePasswordAsync(
        Guid id, ChangePasswordInput input, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(input.CurrentPassword) ||
            input.CurrentPassword.Length > 128 ||
            string.IsNullOrEmpty(input.NewPassword) ||
            input.NewPassword.Length > 128)
        {
            throw new CompletionException(400, "طول گذرواژه معتبر نیست.");
        }

        var user = await User(id, ct);
        user.SessionVersion++;
        user.MustChangePassword = false;

        Check(await users.ChangePasswordAsync(
            user, input.CurrentPassword, input.NewPassword));

        return true;
    }

    public async Task<SettingsView> SettingsAsync(CancellationToken ct)
    {
        var p = await db.Set<AuthenticationPolicy>()
            .AsNoTracking().SingleAsync(x => x.Id == 1, ct);

        return new(new AuthSettingsInput
        {
            PublicRegistrationEnabled = p.PublicRegistrationEnabled,
            RegistrationStartsUtc = p.RegistrationStartsUtc,
            RegistrationEndsUtc = p.RegistrationEndsUtc,
            LoginMode = p.LoginMode,
            RecoveryEnabled = p.RecoveryEnabled,
            RecoveryChannel = p.RecoveryChannel,
            Version = p.Version
        },
        sender.IsConfigured(DeliveryChannel.Email),
        sender.IsConfigured(DeliveryChannel.Sms));
    }

    public async Task<bool> SaveSettingsAsync(
        Guid actor, AuthSettingsInput input, CancellationToken ct)
    {
        if (!Enum.IsDefined(input.LoginMode) ||
            !Enum.IsDefined(input.RecoveryChannel))
            throw new CompletionException(400, "روش احراز هویت معتبر نیست.");

        if (input.PublicRegistrationEnabled &&
            (!input.RegistrationStartsUtc.HasValue ||
             !input.RegistrationEndsUtc.HasValue ||
             input.RegistrationStartsUtc >= input.RegistrationEndsUtc))
        {
            throw new CompletionException(
                400, "بازهٔ شروع و پایان ثبت‌نام معتبر نیست.");
        }

        var admin = await User(actor, ct);

        if (input.LoginMode != LoginMode.Password)
        {
            var channel = input.LoginMode == LoginMode.PasswordAndEmail
                ? DeliveryChannel.Email : DeliveryChannel.Sms;

            if (!sender.IsConfigured(channel))
                throw new CompletionException(
                    400, "سرویس ارسال این کانال پیکربندی نشده است.");

            if (channel == DeliveryChannel.Email
                    ? !admin.EmailConfirmed
                    : !admin.PhoneNumberConfirmed)
            {
                throw new CompletionException(
                    400, "ابتدا کانال حساب ادمین را در بخش امنیت حساب تأیید کنید.");
            }
        }

        if (input.RecoveryEnabled &&
            !sender.IsConfigured(input.RecoveryChannel))
            throw new CompletionException(
                400, "سرویس بازیابی پیکربندی نشده است.");

        await using var tx = await db.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var p = await db.Set<AuthenticationPolicy>()
            .SingleAsync(x => x.Id == 1, ct);

        if (p.Version != input.Version)
            throw new CompletionException(
                409, "تنظیمات هم‌زمان تغییر کرده‌اند؛ دوباره بارگذاری کنید.");

        var invalidate = p.LoginMode != input.LoginMode;

        p.PublicRegistrationEnabled = input.PublicRegistrationEnabled;
        p.RegistrationStartsUtc = input.RegistrationStartsUtc?.ToUniversalTime();
        p.RegistrationEndsUtc = input.RegistrationEndsUtc?.ToUniversalTime();
        p.LoginMode = input.LoginMode;
        p.RecoveryEnabled = input.RecoveryEnabled;
        p.RecoveryChannel = input.RecoveryChannel;
        p.Version++;

        if (invalidate)
        {
            await db.Users.ExecuteUpdateAsync(
                s => s.SetProperty(
                    x => x.SessionVersion, x => x.SessionVersion + 1), ct);
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(
        string kind, Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, ct);

        if (kind == "role")
        {
            var role = await db.Roles.SingleOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new CompletionException(404, "نقش پیدا نشد.");

            if (await db.Set<UserRoleAssignment>().AnyAsync(x => x.RoleId == id, ct) ||
                await db.Set<UserAccessBundleRole>().AnyAsync(x => x.RoleId == id, ct))
            {
                throw new CompletionException(
                    409, "نقش دارای تخصیص است و قابل حذف نیست.");
            }

            db.RemoveRange(await db.Set<RolePermission>()
                .Where(x => x.RoleId == id).ToListAsync(ct));

            db.Remove(role);
        }
       
        else
        {
            throw new CompletionException(400, "نوع موجودیت معتبر نیست.");
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<PageResult<AuditRow>> AuditAsync(
        int page, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        var q = db.Set<SecurityAuditEntry>().AsNoTracking();
        var total = await q.CountAsync(ct);

        var rows = await q.OrderByDescending(x => x.OccurredUtc)
            .Skip((page - 1) * 30).Take(30)
            .Select(x => new AuditRow(
                x.Id, x.ActorId, x.OccurredUtc,
                x.Action, x.Entity, x.EntityId, x.ChangedFields))
            .ToListAsync(ct);

        return new(rows, total, page, 30);
    }

    public async Task<IReadOnlyList<BundleRow>> BundlesAsync(
        Guid id, CancellationToken ct)
    {
        var rows = await db.Set<UserAccessBundle>()
            .AsNoTracking()
            .Include(x => x.Roles).Include(x => x.Scopes)
            .Where(x => x.UserId == id)
            .OrderBy(x => x.SortOrder).ToListAsync(ct);

        return rows.Select(x => new BundleRow
        {
            BundleId = x.Id,
            Name = x.Name,
            RoleIds = x.Roles.Select(r => r.RoleId).ToArray(),
            ScopeIds = x.Scopes.Select(s => s.ScopeId).ToArray(),
            ScopeMode = x.ScopeSelectionMode == ScopeSelectionMode.All
                ? "all" : "selected",
            IsEnabled = x.IsEnabled,
            SortOrder = x.SortOrder
        }).ToArray();
    }

    public async Task<Guid> AssignAsync(
        Guid userId, Guid roleId, Guid scopeId, CancellationToken ct)
    {
        await using var tx = await db.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, ct);

        await User(userId, ct);

        var role = await db.Roles.SingleOrDefaultAsync(x => x.Id == roleId, ct)
            ?? throw new CompletionException(404, "نقش پیدا نشد.");

        var scope = await db.Set<AccessScope>()
            .SingleOrDefaultAsync(x => x.Id == scopeId, ct)
            ?? throw new CompletionException(404, "محدوده پیدا نشد.");

        // موتور فعلی بعد از وجود Bundle، legacy pairها را نادیده می‌گیرد.
        // قبل از ساخت اولین Bundle، pairهای فعال را منتقل می‌کنیم.
        if (!await db.Set<UserAccessBundle>().AnyAsync(x => x.UserId == userId, ct))
        {
            var old = await db.Set<UserRoleAssignment>()
                .Where(x => x.UserId == userId && x.IsEnabled)
                .ToListAsync(ct);

            foreach (var pair in old)
                db.Add(new UserAccessBundle(
                    userId, "تخصیص منتقل‌شده", [pair.RoleId],
                    ScopeSelectionMode.Selected, [pair.ScopeId]));

            await db.SaveChangesAsync(ct);
        }

        var bundles = await db.Set<UserAccessBundle>()
            .Include(x => x.Roles).Include(x => x.Scopes)
            .Where(x => x.UserId == userId).ToListAsync(ct);

        var match = bundles.FirstOrDefault(x =>
            x.ScopeSelectionMode == ScopeSelectionMode.Selected &&
            x.Roles.Count == 1 && x.Scopes.Count == 1 &&
            x.Roles.Any(r => r.RoleId == roleId) &&
            x.Scopes.Any(s => s.ScopeId == scopeId));

        if (match is null)
        {
            var name = $"{role.Code} / {scope.Code}";

            match = new UserAccessBundle(
                userId, name[..Math.Min(name.Length, 200)], [roleId],
                ScopeSelectionMode.Selected, [scopeId]);

            db.Add(match);
        }
        else
        {
            match.Enable();
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return match.Id;
    }

    private async Task<Guid?> Scope(
        string mode, string? code, CancellationToken ct)
    {
        if (string.Equals(mode, "all", StringComparison.OrdinalIgnoreCase))
            return null;

        if (!string.IsNullOrWhiteSpace(mode) &&
            !string.Equals(mode, "selected", StringComparison.OrdinalIgnoreCase))
            throw new CompletionException(400, "حالت محدوده معتبر نیست.");

        return await db.Set<AccessScope>()
            .Where(x => x.Code == code)
            .Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct)
            ?? throw new CompletionException(404, "محدوده پیدا نشد.");
    }

    public async Task<Guid> WriteWorkItemAsync(
        Guid? id, string mode, string? code,
        WorkItemInput input, CancellationToken ct)
    {
        var allowed = await Scope(mode, code, ct);

        if (string.IsNullOrWhiteSpace(input.Title) ||
            input.Title.Trim().Length > 200 ||
            input.Status is not ("Open" or "Closed"))
            throw new CompletionException(400, "عنوان یا وضعیت معتبر نیست.");

        WorkItem entity;

        if (id.HasValue)
        {
            entity = await db.Set<WorkItem>().SingleOrDefaultAsync(
                x => x.Id == id &&
                     (!allowed.HasValue || x.ScopeId == allowed.Value), ct)
                ?? throw new CompletionException(404, "مورد کاری پیدا نشد.");

            if (input.ScopeId != Guid.Empty && input.ScopeId != entity.ScopeId)
                throw new CompletionException(
                    400, "انتقال مورد کاری بین محدوده‌ها مجاز نیست.");

            entity.Update(input.Title, input.Status);
        }
        else
        {
            var target = allowed ?? input.ScopeId;

            if (allowed.HasValue &&
                input.ScopeId != Guid.Empty &&
                input.ScopeId != allowed.Value)
                throw new CompletionException(403, "محدوده خارج از پروفایل فعال است.");

            if (!await db.Set<AccessScope>().AnyAsync(x => x.Id == target, ct))
                throw new CompletionException(400, "محدودهٔ مقصد معتبر نیست.");

            entity = new WorkItem(target, input.Title, input.Status);
            db.Add(entity);
        }

        await db.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task<bool> DeleteWorkItemAsync(
        Guid id, string mode, string? code, CancellationToken ct)
    {
        var allowed = await Scope(mode, code, ct);

        var entity = await db.Set<WorkItem>().SingleOrDefaultAsync(
            x => x.Id == id &&
                 (!allowed.HasValue || x.ScopeId == allowed.Value), ct)
            ?? throw new CompletionException(404, "مورد کاری پیدا نشد.");

        db.Remove(entity);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<WorkItemRow>> ListWorkItemsAsync(
        string mode, string? code, CancellationToken ct)
    {
        var scope = await Scope(mode, code, ct);
        var q = db.Set<WorkItem>().AsNoTracking();

        if (scope.HasValue)
            q = q.Where(x => x.ScopeId == scope.Value);

        return await q.OrderBy(x => x.Title)
            .Select(x => new WorkItemRow(x.Id, x.Title, x.Status))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ScopeRow>> WritableScopesAsync(
        string mode, string? code, CancellationToken ct)
    {
        var scope = await Scope(mode, code, ct);
        var q = db.Set<AccessScope>().AsNoTracking();

        if (scope.HasValue)
            q = q.Where(x => x.Id == scope.Value);

        return await q.OrderBy(x => x.DisplayName)
            .Select(x => new ScopeRow(
                x.Id, x.Code, x.DisplayName, x.ParentScopeId))
            .ToListAsync(ct);
    }

    public async Task<ReportSummary> ReportAsync(
        string mode, string? code, CancellationToken ct)
    {
        var scope = await Scope(mode, code, ct);
        var q = db.Set<WorkItem>().AsNoTracking();

        if (scope.HasValue)
            q = q.Where(x => x.ScopeId == scope.Value);

        return new(
            await q.CountAsync(ct),
            await q.CountAsync(x => x.Status == "Open", ct),
            await q.CountAsync(x => x.Status == "Closed", ct));
    }
}