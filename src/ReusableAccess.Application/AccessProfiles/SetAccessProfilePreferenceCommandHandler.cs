using MediatR;
using ReusableAccess.Application.Accounts;
using ReusableAccess.Domain.Enums;
namespace ReusableAccess.Application.AccessProfiles;
public sealed class SetAccessProfilePreferenceCommandHandler(IAccountRepository accounts, IAccessProfilePreferenceRepository preferences)
    : IRequestHandler<SetAccessProfilePreferenceCommand, AccessProfilePreferenceSelection?>
{
    public async Task<AccessProfilePreferenceSelection?> Handle(SetAccessProfilePreferenceCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (await accounts.FindByIdAsync(request.UserId, cancellationToken) is null) return null;
        var bundle = await preferences.FindOwnedEnabledBundleAsync(request.BundleId, request.UserId, cancellationToken);
        if (bundle is null || bundle.ScopeSelectionMode != request.ScopeMode) return null;
        var roleId = await preferences.FindRoleIdAsync(bundle.Id, request.RoleCode, cancellationToken);
        if (roleId == Guid.Empty) return null;
        Guid? scopeId = null;
        if (request.ScopeMode == ScopeSelectionMode.Selected)
        {
            scopeId = await preferences.FindScopeIdAsync(bundle.Id, request.ScopeCode, cancellationToken);
            if (scopeId is null) return null;
        }
        var selection = new AccessProfilePreferenceSelection(bundle.Id, roleId, scopeId);
        preferences.SetSelection(request.UserId, selection);
        await preferences.SaveChangesAsync(cancellationToken);
        return selection;
    }
}
