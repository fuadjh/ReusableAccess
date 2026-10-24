using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Bundles;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Domain.Enums;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure.Bundles;

public sealed class UserBundleCommandStore(
    ApplicationDbContext dbContext) : IUserBundleCommandStore
{
    public async Task<BundleCreationResult> CreateAsync(
        CreateUserBundleCommand command,
        CancellationToken cancellationToken)
    {
        var userExists = await dbContext.Users
            .AnyAsync(user => user.Id == command.UserId, cancellationToken);

        if (!userExists)
        {
            return new BundleCreationResult(
                BundleCreationStatus.UserNotFound);
        }

        var error = await ValidateRequestAsync(
            command,
            cancellationToken);

        if (error is not null)
        {
            return new BundleCreationResult(
                BundleCreationStatus.InvalidRequest,
                ErrorMessage: error);
        }

        var scopeMode = string.Equals(
            command.ScopeMode,
            "all",
            StringComparison.OrdinalIgnoreCase)
                ? ScopeSelectionMode.All
                : ScopeSelectionMode.Selected;

        var bundle = new UserAccessBundle(
            command.UserId,
            command.Name!.Trim(),
            command.RoleIds!,
            scopeMode,
            command.ScopeIds!,
            command.SortOrder);

        dbContext.Set<UserAccessBundle>().Add(bundle);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new BundleCreationResult(
            BundleCreationStatus.Created,
            BundleId: bundle.Id);
    }

    private async Task<string?> ValidateRequestAsync(
        CreateUserBundleCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name)
            || request.Name.Trim().Length > 200
            || request.SortOrder < 0
            || request.RoleIds is null
            || request.RoleIds.Length == 0
            || request.RoleIds.Any(id => id == Guid.Empty)
            || request.ScopeIds is null
            || request.ScopeIds.Any(id => id == Guid.Empty))
        {
            return "اطلاعات Bundle معتبر نیست.";
        }

        var allMode = string.Equals(
            request.ScopeMode,
            "all",
            StringComparison.OrdinalIgnoreCase);

        var selectedMode = string.Equals(
            request.ScopeMode,
            "selected",
            StringComparison.OrdinalIgnoreCase);

        if (!allMode && !selectedMode)
        {
            return "scopeMode باید all یا selected باشد.";
        }

        if (allMode && request.ScopeIds.Length > 0)
        {
            return "در حالت all نباید Scope مشخص ارسال شود.";
        }

        if (selectedMode && request.ScopeIds.Length == 0)
        {
            return "در حالت selected دست‌کم یک Scope لازم است.";
        }

        var roleIds = request.RoleIds.Distinct().ToArray();
        var scopeIds = request.ScopeIds.Distinct().ToArray();

        if (await dbContext.Set<Role>().CountAsync(
                role => roleIds.Contains(role.Id),
                cancellationToken) != roleIds.Length)
        {
            return "یک یا چند Role پیدا نشد.";
        }

        if (await dbContext.Set<AccessScope>().CountAsync(
                scope => scopeIds.Contains(scope.Id),
                cancellationToken) != scopeIds.Length)
        {
            return "یک یا چند Scope پیدا نشد.";
        }

        return null;
    }
    public async Task<BundleUpdateResult> UpdateAsync(
    UpdateUserBundleCommand command,
    CancellationToken cancellationToken)
    {
        var bundle = await dbContext.Set<UserAccessBundle>()
            .Include(item => item.Roles)
            .Include(item => item.Scopes)
            .SingleOrDefaultAsync(
                item => item.Id == command.BundleId,
                cancellationToken);

        if (bundle is null)
        {
            return new BundleUpdateResult(
                BundleUpdateStatus.BundleNotFound);
        }

        var validationCommand = new CreateUserBundleCommand(
            UserId: bundle.UserId,
            Name: command.Name,
            RoleIds: command.RoleIds,
            ScopeMode: command.ScopeMode,
            ScopeIds: command.ScopeIds,
            SortOrder: command.SortOrder,
            IsEnabled: command.IsEnabled);

        var error = await ValidateRequestAsync(
            validationCommand,
            cancellationToken);

        if (error is not null)
        {
            return new BundleUpdateResult(
                BundleUpdateStatus.InvalidRequest,
                ErrorMessage: error);
        }

        var scopeMode = string.Equals(
            command.ScopeMode,
            "all",
            StringComparison.OrdinalIgnoreCase)
                ? ScopeSelectionMode.All
                : ScopeSelectionMode.Selected;

        bundle.Rename(command.Name!.Trim());
        bundle.ChangeSortOrder(command.SortOrder);
        bundle.ReplaceDefinition(
            command.RoleIds!,
            scopeMode,
            command.ScopeIds!);

        if (command.IsEnabled)
        {
            bundle.Enable();
        }
        else
        {
            bundle.Disable();
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new BundleUpdateResult(BundleUpdateStatus.Updated);
    }
}