using MediatR;

namespace ReusableAccess.Application.Bundles;

public sealed record UpdateUserBundleCommand(
    Guid BundleId,
    string? Name,
    Guid[]? RoleIds,
    string? ScopeMode,
    Guid[]? ScopeIds,
    int SortOrder,
    bool IsEnabled)
    : IRequest<BundleUpdateResult>;

public enum BundleUpdateStatus
{
    Updated,
    BundleNotFound,
    InvalidRequest
}

public sealed record BundleUpdateResult(
    BundleUpdateStatus Status,
    string? ErrorMessage = null);

public sealed class UpdateUserBundleCommandHandler(
    IUserBundleRepository repository)
    : IRequestHandler<UpdateUserBundleCommand, BundleUpdateResult>
{
    public async Task<BundleUpdateResult> Handle(
        UpdateUserBundleCommand request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var bundle = await repository.FindForUpdateAsync(
            request.BundleId,
            cancellationToken);

        if (bundle is null)
        {
            return new BundleUpdateResult(
                BundleUpdateStatus.BundleNotFound);
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
            return new BundleUpdateResult(
                BundleUpdateStatus.InvalidRequest,
                ErrorMessage: validation.Error);
        }

        // تا پایان اعتبارسنجی، وضعیت موجودیت را تغییر نمی‌دهیم.
        bundle.Rename(request.Name!.Trim());
        bundle.ChangeSortOrder(request.SortOrder);
        bundle.ReplaceDefinition(
            request.RoleIds!,
            validation.Mode,
            request.ScopeIds!);

        if (request.IsEnabled)
        {
            bundle.Enable();
        }
        else
        {
            bundle.Disable();
        }

        // تمام تغییرات این Use Case با یک فراخوانی ذخیره می‌شوند.
        await repository.SaveChangesAsync(cancellationToken);

        return new BundleUpdateResult(BundleUpdateStatus.Updated);
    }
}