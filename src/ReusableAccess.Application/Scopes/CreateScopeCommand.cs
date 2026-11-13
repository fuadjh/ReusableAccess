using MediatR;
using ReusableAccess.Application.Persistence;
using ReusableAccess.Domain.Entities;

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

public sealed class CreateScopeCommandHandler(IScopeRepository repository)
    : IRequestHandler<CreateScopeCommand, ScopeCreationResult>
{
    public async Task<ScopeCreationResult> Handle(
      CreateScopeCommand request,
      CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalized = ScopeCommandValidation.Normalize(request);

        var codeExists = await repository.ExistsByCodeAsync(
            normalized.Code,
            cancellationToken);

        if (codeExists)
        {
            return new ScopeCreationResult(
                ScopeCreationStatus.CodeConflict,
                Data: null);
        }

        if (normalized.ParentScopeId is Guid parentId)
        {
            var parentExists = await repository.ExistsByIdAsync(
                parentId,
                cancellationToken);

            if (!parentExists)
            {
                return new ScopeCreationResult(
                    ScopeCreationStatus.ParentNotFound,
                    Data: null);
            }
        }

        var scope = new AccessScope(
            normalized.Code,
            normalized.DisplayName,
            normalized.ParentScopeId);

        repository.Add(scope);

        try
        {
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateCodeException error) when (
            error.Owner == CodeOwner.Scope
            && error.Code == scope.Code)
        {
            return new ScopeCreationResult(
                ScopeCreationStatus.CodeConflict,
                Data: null);
        }

        return new ScopeCreationResult(
            ScopeCreationStatus.Created,
            new ScopeCreatedDetails(
                scope.Id,
                scope.Code,
                scope.DisplayName,
                scope.ParentScopeId));
    }
}