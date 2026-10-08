using System;

namespace ReusableAccess.Domain.Entities;

public sealed class AccessScope
{
    private AccessScope()
    {
    }

    public AccessScope(
        string code,
        string displayName,
        Guid? parentScopeId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        if (parentScopeId == Guid.Empty)
        {
            throw new ArgumentException(
                "شناسهٔ محدودهٔ والد نباید خالی باشد.",
                nameof(parentScopeId));
        }

        Id = Guid.NewGuid();
        Code = code.Trim();
        DisplayName = displayName.Trim();
        ParentScopeId = parentScopeId;
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public Guid? ParentScopeId { get; private set; }
}