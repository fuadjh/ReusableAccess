using MediatR;
using ReusableAccess.Domain.Entities;

namespace ReusableAccess.Application.Bundles;

public sealed record CreateUserBundleCommand(
    Guid UserId,
    string? Name,
    Guid[]? RoleIds,
    string? ScopeMode,
    Guid[]? ScopeIds,
    int SortOrder,
    bool IsEnabled)
    : IRequest<BundleCreationResult>;

public enum BundleCreationStatus
{
    Created,
    UserNotFound,
    InvalidRequest
}

public sealed record BundleCreationResult(
    BundleCreationStatus Status,
    Guid? BundleId = null,
    string? ErrorMessage = null);

public sealed class CreateUserBundleCommandHandler(
    IUserBundleRepository repository)
    : IRequestHandler<CreateUserBundleCommand, BundleCreationResult>
{
    public async Task<BundleCreationResult> Handle(
        CreateUserBundleCommand request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // ترتیب فعلی حفظ می‌شود: وجود کاربر قبل از اعتبارسنجی تعریف.
        var userExists = await repository.UserExistsAsync(
            request.UserId,
            cancellationToken);

        if (!userExists)
        {
            return new BundleCreationResult(
                BundleCreationStatus.UserNotFound);
        }

        var validation = await UserBundleValidation.ValidateAsync(
            request.Name,
            request.RoleIds,
            request.ScopeMode,
            request.ScopeIds,
            request.SortOrder,
            repository,
            cancellationToken);

        if (validation.Error is not null)
        {
            return new BundleCreationResult(
                BundleCreationStatus.InvalidRequest,
                ErrorMessage: validation.Error);
        }

        var bundle = new UserAccessBundle(
            request.UserId,
            request.Name!.Trim(),
            request.RoleIds!,
            validation.Mode,
            request.ScopeIds!,
            request.SortOrder);

        if (!request.IsEnabled)
        {
            bundle.Disable();
        }

        repository.Add(bundle);

        await repository.SaveChangesAsync(cancellationToken);

        return new BundleCreationResult(
            BundleCreationStatus.Created,
            BundleId: bundle.Id);
    }
}