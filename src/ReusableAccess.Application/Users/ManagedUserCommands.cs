using MediatR;
using ReusableAccess.Application.Completion;
using ReusableAccess.Contracts;

namespace ReusableAccess.Application.Users;

public sealed record CreateManagedUser(
    Guid ActorId,
    CreateUserInput Input) : IRequest<Guid>;

public sealed record UpdateManagedUser(
    Guid ActorId,
    Guid UserId,
    UpdateUserInput Input) : IRequest<bool>;

public sealed record SetManagedUserActive(
    Guid ActorId,
    Guid UserId,
    ActiveUserInput Input) : IRequest<bool>;

public sealed record ResetManagedPassword(
    Guid ActorId,
    Guid UserId,
    string Password) : IRequest<bool>;

public sealed class CreateManagedUserHandler(IManagedUserCommandStore store)
    : IRequestHandler<CreateManagedUser, Guid>
{
    public Task<Guid> Handle(
        CreateManagedUser request,
        CancellationToken cancellationToken)
    {
        CompletionValidation.Profile(request.Input);

        return store.CreateManagedAsync(
            request.Input,
            cancellationToken);
    }
}

public sealed class UpdateManagedUserHandler(IManagedUserCommandStore store)
    : IRequestHandler<UpdateManagedUser, bool>
{
    public Task<bool> Handle(
        UpdateManagedUser request,
        CancellationToken cancellationToken)
    {
        CompletionValidation.Profile(request.Input);

        return store.UpdateUserAsync(
            request.ActorId,
            request.UserId,
            request.Input,
            cancellationToken);
    }
}

public sealed class SetManagedUserActiveHandler(IManagedUserCommandStore store)
    : IRequestHandler<SetManagedUserActive, bool>
{
    public Task<bool> Handle(
        SetManagedUserActive request,
        CancellationToken cancellationToken)
        => store.SetActiveAsync(
            request.ActorId,
            request.UserId,
            request.Input,
            cancellationToken);
}

public sealed class ResetManagedPasswordHandler(IManagedUserCommandStore store)
    : IRequestHandler<ResetManagedPassword, bool>
{
    public Task<bool> Handle(
        ResetManagedPassword request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Password) ||
            request.Password.Length is < 12 or > 128)
        {
            throw new CompletionException(
                400, "گذرواژه باید بین ۱۲ تا ۱۲۸ کاراکتر باشد.");
        }

        return store.ResetPasswordAsync(
            request.ActorId,
            request.UserId,
            request.Password,
            cancellationToken);
    }
}