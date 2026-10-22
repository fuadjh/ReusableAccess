using ReusableAccess.Domain.Enums;

namespace ReusableAccess.Domain.Entities;

public sealed class UserAccessBundle
{
    private readonly List<UserAccessBundleRole> _roles = [];
    private readonly List<UserAccessBundleScope> _scopes = [];

    private UserAccessBundle()
    {
    }

    public UserAccessBundle(
        Guid userId,
        string name,
        IEnumerable<Guid> roleIds,
        ScopeSelectionMode scopeSelectionMode,
        IEnumerable<Guid>? scopeIds = null, 
        int sortOrder = 0)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "شناسهٔ کاربر نباید خالی باشد.",
                nameof(userId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder));
        }
        Id = Guid.NewGuid();
        UserId = userId;
        Name = name.Trim();
        SortOrder = sortOrder;
        ReplaceDefinition(roleIds, scopeSelectionMode, scopeIds);
        IsEnabled = true;
        
       

    }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        name = name.Trim();

        if (name.Length > 200)
        {
            throw new ArgumentOutOfRangeException(
                nameof(name),
                "نام Bundle نباید بیشتر از ۲۰۰ کاراکتر باشد.");
        }

        Name = name;
    }


    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }
    public int SortOrder { get; private set; }

    public void ChangeSortOrder(int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder));
        }

        SortOrder = sortOrder;
    }
    public string Name { get; private set; } = string.Empty;

    public ScopeSelectionMode ScopeSelectionMode { get; private set; }

    public bool IsEnabled { get; private set; }
   

   
    public IReadOnlyCollection<UserAccessBundleRole> Roles =>
        _roles.AsReadOnly();

    public IReadOnlyCollection<UserAccessBundleScope> Scopes =>
        _scopes.AsReadOnly();

    public void ReplaceDefinition(
        IEnumerable<Guid> roleIds,
        ScopeSelectionMode scopeSelectionMode,
        IEnumerable<Guid>? scopeIds = null)
    {
        ArgumentNullException.ThrowIfNull(roleIds);

        if (!Enum.IsDefined(scopeSelectionMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(scopeSelectionMode));
        }

        var validatedRoleIds = roleIds.ToArray();

        if (validatedRoleIds.Length == 0)
        {
            throw new ArgumentException(
                "بسته باید دست‌کم یک نقش داشته باشد.",
                nameof(roleIds));
        }

        if (validatedRoleIds.Any(id => id == Guid.Empty))
        {
            throw new ArgumentException(
                "شناسهٔ نقش نباید خالی باشد.",
                nameof(roleIds));
        }

        var validatedScopeIds = (scopeIds ?? []).ToArray();

        if (validatedScopeIds.Any(id => id == Guid.Empty))
        {
            throw new ArgumentException(
                "شناسهٔ اسکوپ نباید خالی باشد.",
                nameof(scopeIds));
        }

        if (scopeSelectionMode == ScopeSelectionMode.Selected &&
            validatedScopeIds.Length == 0)
        {
            throw new ArgumentException(
                "در حالت انتخاب اسکوپ، دست‌کم یک اسکوپ لازم است.",
                nameof(scopeIds));
        }

        if (scopeSelectionMode == ScopeSelectionMode.All &&
            validatedScopeIds.Length > 0)
        {
            throw new ArgumentException(
                "در حالت همهٔ اسکوپ‌ها، نباید اسکوپ مشخصی ارسال شود.",
                nameof(scopeIds));
        }

        _roles.Clear();
        _scopes.Clear();

        foreach (var roleId in validatedRoleIds.Distinct())
        {
            _roles.Add(new UserAccessBundleRole(Id, roleId));
        }

        foreach (var scopeId in validatedScopeIds.Distinct())
        {
            _scopes.Add(new UserAccessBundleScope(Id, scopeId));
        }

        ScopeSelectionMode = scopeSelectionMode;
    }

    public bool Allows(Guid roleId, Guid scopeId)
    {
        if (roleId == Guid.Empty || scopeId == Guid.Empty)
        {
            return false;
        }

        var containsRole = _roles.Any(role => role.RoleId == roleId);

        if (!containsRole)
        {
            return false;
        }

        return ScopeSelectionMode == ScopeSelectionMode.All ||
               _scopes.Any(scope => scope.ScopeId == scopeId);
    }

    public void Disable()
    {
        IsEnabled = false;
    }

    public void Enable()
    {
        IsEnabled = true;
    }
}