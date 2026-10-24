using MediatR;
using ReusableAccess.Domain.Enums;

namespace ReusableAccess.Application.AccessProfiles;

public sealed record SetAccessProfilePreferenceCommand(
    Guid UserId,
    Guid BundleId,
    string RoleCode,
    string? ScopeCode,
    ScopeSelectionMode ScopeMode)
    : IRequest<AccessProfilePreferenceSelection?>;

public sealed record AccessProfilePreferenceSelection(
    Guid BundleId,
    Guid RoleId,
    Guid? ScopeId);

public interface IAccessProfilePreferenceStore
{
    Task<AccessProfilePreferenceSelection?> SaveAsync(
        SetAccessProfilePreferenceCommand command,
        CancellationToken cancellationToken);
}