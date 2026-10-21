using Microsoft.AspNetCore.Authentication;

namespace ReusableAccess.Web.Infrastructure;

public sealed record ActiveAccessProfile(
    Guid BundleId,
    string RoleCode,
    string? ScopeCode,
    string ScopeMode);

public sealed class ActiveAccessProfileStore
{
    private const string BundleIdTicketKey =
        "ReusableAccess.ActiveBundleId";

    private const string ActiveRoleTicketKey =
        "ReusableAccess.ActiveRole";

    private const string ActiveScopeTicketKey =
        "ReusableAccess.ActiveScope";

    private const string ActiveScopeModeTicketKey =
        "ReusableAccess.ActiveScopeMode";

    public void Set(
        AuthenticationProperties properties,
        Guid bundleId,
        string roleCode,
        string? scopeCode,
        string scopeMode)
    {
        ArgumentNullException.ThrowIfNull(properties);

        if (bundleId == Guid.Empty)
        {
            throw new ArgumentException(
                "شناسهٔ بسته نباید خالی باشد.",
                nameof(bundleId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(roleCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeMode);

        if (string.Equals(
                scopeMode,
                "selected",
                StringComparison.OrdinalIgnoreCase))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(scopeCode);
        }
        else if (string.Equals(
                     scopeMode,
                     "all",
                     StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(scopeCode))
            {
                throw new ArgumentException(
                    "در حالت all نباید اسکوپ مشخص ارسال شود.",
                    nameof(scopeCode));
            }
        }
        else
        {
            throw new ArgumentException(
                "حالت اسکوپ باید selected یا all باشد.",
                nameof(scopeMode));
        }

        properties.Items[BundleIdTicketKey] = bundleId.ToString();
        properties.Items[ActiveRoleTicketKey] = roleCode;
        properties.Items[ActiveScopeTicketKey] = scopeCode ?? string.Empty;
        properties.Items[ActiveScopeModeTicketKey] =
            scopeMode.ToLowerInvariant();
    }

    public bool TryGet(
        AuthenticationProperties? properties,
        out ActiveAccessProfile profile)
    {
        profile = default!;

        if (properties is null ||
            !properties.Items.TryGetValue(
                BundleIdTicketKey,
                out var bundleIdValue) ||
            !Guid.TryParse(bundleIdValue, out var bundleId) ||
            bundleId == Guid.Empty ||
            !properties.Items.TryGetValue(
                ActiveRoleTicketKey,
                out var roleCode) ||
            string.IsNullOrWhiteSpace(roleCode) ||
            !properties.Items.TryGetValue(
                ActiveScopeModeTicketKey,
                out var scopeMode) ||
            string.IsNullOrWhiteSpace(scopeMode) ||
            !properties.Items.TryGetValue(
                ActiveScopeTicketKey,
                out var rawScopeCode))
        {
            return false;
        }

        var scopeCode = string.IsNullOrWhiteSpace(rawScopeCode)
            ? null
            : rawScopeCode;

        if (string.Equals(
                scopeMode,
                "selected",
                StringComparison.OrdinalIgnoreCase))
        {
            if (scopeCode is null)
            {
                return false;
            }
        }
        else if (string.Equals(
                     scopeMode,
                     "all",
                     StringComparison.OrdinalIgnoreCase))
        {
            if (scopeCode is not null)
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        profile = new ActiveAccessProfile(
            bundleId,
            roleCode,
            scopeCode,
            scopeMode.ToLowerInvariant());

        return true;
    }
}