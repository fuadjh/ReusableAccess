using MediatR;

namespace ReusableAccess.Application.Scopes;

public sealed record RenameScopeCommand(
    Guid ScopeId,
    string DisplayName)
    : IRequest<ScopeRenameResult?>;

public sealed record ScopeRenameResult(
    Guid ScopeId,
    string ScopeCode,
    string DisplayName,
    Guid? ParentScopeId);