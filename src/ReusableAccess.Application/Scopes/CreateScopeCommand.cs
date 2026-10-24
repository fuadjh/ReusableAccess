using MediatR;

namespace ReusableAccess.Application.Scopes;

public sealed record CreateScopeCommand(
    string Code,
    string DisplayName,
    Guid? ParentScopeId)
    : IRequest<ScopeCreationResult>;

public enum ScopeCreationStatus
{
    Created,
    CodeConflict,
    ParentNotFound
}

public sealed record ScopeCreatedDetails(
    Guid ScopeId,
    string ScopeCode,
    string DisplayName,
    Guid? ParentScopeId);

public sealed record ScopeCreationResult(
    ScopeCreationStatus Status,
    ScopeCreatedDetails? Data);

public interface IScopeCommandStore
{
    Task<ScopeCreationResult> CreateAsync(
        CreateScopeCommand command,
        CancellationToken cancellationToken);
    Task<ScopeRenameResult?> RenameAsync(
    Guid scopeId,
    string displayName,
    CancellationToken cancellationToken);
}