using ReusableAccess.Contracts;
using ReusableAccess.Domain.Identity;

namespace ReusableAccess.Application.Accounts;

// این‌ها همان قراردادهای موجود هستند که به محل صحیح منتقل می‌شوند.
// نسخهٔ موازی یا alias برای Namespace قدیمی ایجاد نمی‌کنیم.

public interface IAccountSessionValidator
{
    Task<bool> IsCurrentAsync(
        Guid userId,
        int sessionVersion,
        CancellationToken ct);
}

public interface IAccountWorkflow
{
    Task<PublicAuthOptions> OptionsAsync(CancellationToken ct);

    Task<LoginOutcome> PasswordAsync(
        string nationalId,
        string password,
        bool remember,
        CancellationToken ct);

    Task<LoginOutcome> VerifyLoginAsync(
        Guid challenge,
        string code,
        CancellationToken ct);

    Task<LoginOutcome> ChangeRequiredPasswordAsync(
        Guid challenge,
        string password,
        CancellationToken ct);

    Task RegisterAsync(
        CreateUserInput input,
        CancellationToken ct);

    Task<Guid> RequestRecoveryAsync(
        string nationalId,
        CancellationToken ct);

    Task ResetRecoveredPasswordAsync(
        Guid challenge,
        string code,
        string password,
        CancellationToken ct);

    Task<Guid> RequestContactAsync(
        Guid userId,
        DeliveryChannel channel,
        CancellationToken ct);

    Task ConfirmContactAsync(
        Guid userId,
        Guid challenge,
        string code,
        CancellationToken ct);

    Task SignOutAsync();
}

public interface IAccountMessageSender
{
    bool IsConfigured(DeliveryChannel channel);

    Task SendAsync(
        DeliveryChannel channel,
        string destination,
        string code,
        CancellationToken ct);
}

public interface IRecoveryCodeQueue
{
    bool TryEnqueue(
        DeliveryChannel channel,
        string destination,
        string code);
}