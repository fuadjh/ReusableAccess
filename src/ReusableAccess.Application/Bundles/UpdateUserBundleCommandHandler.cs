using MediatR;

namespace ReusableAccess.Application.Bundles;

public sealed class UpdateUserBundleCommandHandler(
    IUserBundleCommandStore store)
    : IRequestHandler<UpdateUserBundleCommand, BundleUpdateResult>
{
    public Task<BundleUpdateResult> Handle(
        UpdateUserBundleCommand request,
        CancellationToken cancellationToken)
    {
        return store.UpdateAsync(request, cancellationToken);
    }
}