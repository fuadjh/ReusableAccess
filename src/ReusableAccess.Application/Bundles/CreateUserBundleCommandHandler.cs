using MediatR;

namespace ReusableAccess.Application.Bundles;

public sealed class CreateUserBundleCommandHandler(
    IUserBundleCommandStore store)
    : IRequestHandler<CreateUserBundleCommand, BundleCreationResult>
{
    public Task<BundleCreationResult> Handle(
        CreateUserBundleCommand request,
        CancellationToken cancellationToken)
    {
        return store.CreateAsync(request, cancellationToken);
    }
}