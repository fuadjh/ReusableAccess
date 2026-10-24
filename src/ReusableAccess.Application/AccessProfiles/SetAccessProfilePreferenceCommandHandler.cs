using MediatR;

namespace ReusableAccess.Application.AccessProfiles;

public sealed class SetAccessProfilePreferenceCommandHandler(
    IAccessProfilePreferenceStore store)
    : IRequestHandler<
        SetAccessProfilePreferenceCommand,
        AccessProfilePreferenceSelection?>
{
    public Task<AccessProfilePreferenceSelection?> Handle(
        SetAccessProfilePreferenceCommand request,
        CancellationToken cancellationToken)
    {
        return store.SaveAsync(request, cancellationToken);
    }
}