using Microsoft.AspNetCore.Authentication;

namespace ReusableAccess.Web.Infrastructure;

public sealed record ActiveAccessProfile(
    string RoleCode,
    string ScopeCode);

public sealed class ActiveAccessProfileStore
{
    private const string ActiveRoleTicketKey =
        "ReusableAccess.ActiveRole";

    private const string ActiveScopeTicketKey =
        "ReusableAccess.ActiveScope";

    public void Set(
        AuthenticationProperties properties,
        string roleCode,
        string scopeCode)
    {
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentException.ThrowIfNullOrWhiteSpace(roleCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeCode);

        properties.Items[ActiveRoleTicketKey] = roleCode;
        properties.Items[ActiveScopeTicketKey] = scopeCode;
    }

    public bool TryGet(
        AuthenticationProperties? properties,
        out ActiveAccessProfile profile)
    {
        profile = default!;

        if (properties is null ||
            !properties.Items.TryGetValue(
                ActiveRoleTicketKey,
                out var roleCode) ||
            !properties.Items.TryGetValue(
                ActiveScopeTicketKey,
                out var scopeCode) ||
            string.IsNullOrWhiteSpace(roleCode) ||
            string.IsNullOrWhiteSpace(scopeCode))
        {
            return false;
        }

        profile = new ActiveAccessProfile(roleCode, scopeCode);
        return true;
    }
}