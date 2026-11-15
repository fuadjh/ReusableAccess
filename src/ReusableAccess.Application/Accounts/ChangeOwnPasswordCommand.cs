using MediatR;
using ReusableAccess.Application.Errors;
using ReusableAccess.Contracts;
namespace ReusableAccess.Application.Accounts;
public sealed record ChangeOwnPasswordCommand(Guid UserId, ChangePasswordInput Input) : IRequest<bool>, IConcurrencyConflictRequest;
public sealed class ChangeOwnPasswordCommandHandler(IAccountRepository accounts, IAccountIdentityGateway identity)
    : IRequestHandler<ChangeOwnPasswordCommand, bool>
{
    public async Task<bool> Handle(ChangeOwnPasswordCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var input = request.Input;
        if (string.IsNullOrEmpty(input.CurrentPassword) || input.CurrentPassword.Length > 128 ||
            string.IsNullOrEmpty(input.NewPassword) || input.NewPassword.Length > 128)
            throw new UseCaseException(400, "طول گذرواژه معتبر نیست.");
        var state = await accounts.FindByIdAsync(request.UserId, cancellationToken)
            ?? throw new UseCaseException(404, "کاربر پیدا نشد.");
        state.SessionVersion++;
        state.MustChangePassword = false;
        var result = await identity.ChangePasswordAsync(state, input.CurrentPassword, input.NewPassword);
        if (!result.Succeeded)
            throw new UseCaseException(400, string.Join(" ", result.Errors.Select(e => e.Description)));
        return true;
    }
}
