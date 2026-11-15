using MediatR;
using ReusableAccess.Application.Accounts;
using ReusableAccess.Application.Errors;
using ReusableAccess.Application.Persistence;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Domain.Identity;

namespace ReusableAccess.Application.AuthenticationSettings;

public sealed record AccountVerification(bool EmailConfirmed, bool PhoneNumberConfirmed);
public interface IAuthenticationSettingsRepository
{
    Task<AccountVerification?> FindAccountAsync(Guid userId, CancellationToken cancellationToken);
    Task<AuthenticationPolicy> ReadAsync(bool forUpdate, CancellationToken cancellationToken);
    Task InvalidateAllSessionsAsync(CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
public sealed record GetAuthenticationSettingsQuery : IRequest<SettingsView>, IConcurrencyConflictRequest;
public sealed class GetAuthenticationSettingsQueryHandler(
    IAuthenticationSettingsRepository repository, IAccountMessageSender delivery)
    : IRequestHandler<GetAuthenticationSettingsQuery, SettingsView>
{
    public async Task<SettingsView> Handle(GetAuthenticationSettingsQuery request, CancellationToken cancellationToken)
    {
        var p = await repository.ReadAsync(false, cancellationToken);
        return new SettingsView(new AuthSettingsInput
        {
            PublicRegistrationEnabled = p.PublicRegistrationEnabled,
            RegistrationStartsUtc = p.RegistrationStartsUtc,
            RegistrationEndsUtc = p.RegistrationEndsUtc,
            LoginMode = p.LoginMode,
            RecoveryEnabled = p.RecoveryEnabled,
            RecoveryChannel = p.RecoveryChannel,
            Version = p.Version
        }, delivery.IsConfigured(DeliveryChannel.Email), delivery.IsConfigured(DeliveryChannel.Sms));
    }
}
public sealed record UpdateAuthenticationSettingsCommand(Guid ActorId, AuthSettingsInput Input)
    : IRequest<bool>, IConcurrencyConflictRequest;
public sealed class UpdateAuthenticationSettingsCommandHandler(
    IAuthenticationSettingsRepository repository, IAccountMessageSender delivery,
    IWriteTransactionFactory transactions)
    : IRequestHandler<UpdateAuthenticationSettingsCommand, bool>
{
    public async Task<bool> Handle(UpdateAuthenticationSettingsCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var input = request.Input;
        // تقدم خطاها حفظ شده است: ورودی، حساب، کانال، سپس نسخه و ذخیره.
        if (!Enum.IsDefined(input.LoginMode) || !Enum.IsDefined(input.RecoveryChannel))
            throw new UseCaseException(400, "روش احراز هویت معتبر نیست.");
        if (input.PublicRegistrationEnabled &&
            (!input.RegistrationStartsUtc.HasValue || !input.RegistrationEndsUtc.HasValue ||
             input.RegistrationStartsUtc >= input.RegistrationEndsUtc))
            throw new UseCaseException(400, "بازهٔ شروع و پایان ثبت‌نام معتبر نیست.");
        var admin = await repository.FindAccountAsync(request.ActorId, cancellationToken)
            ?? throw new UseCaseException(404, "کاربر پیدا نشد.");
        if (input.LoginMode != LoginMode.Password)
        {
            var channel = input.LoginMode == LoginMode.PasswordAndEmail ? DeliveryChannel.Email : DeliveryChannel.Sms;
            if (!delivery.IsConfigured(channel))
                throw new UseCaseException(400, "سرویس ارسال این کانال پیکربندی نشده است.");
            if (channel == DeliveryChannel.Email ? !admin.EmailConfirmed : !admin.PhoneNumberConfirmed)
                throw new UseCaseException(400, "ابتدا کانال حساب ادمین را در بخش امنیت حساب تأیید کنید.");
        }
        if (input.RecoveryEnabled && !delivery.IsConfigured(input.RecoveryChannel))
            throw new UseCaseException(400, "سرویس بازیابی پیکربندی نشده است.");
        await using var tx = await transactions.BeginSerializableAsync(cancellationToken);
        var p = await repository.ReadAsync(true, cancellationToken);
        if (p.Version != input.Version)
            throw new UseCaseException(409, "تنظیمات هم‌زمان تغییر کرده‌اند؛ دوباره بارگذاری کنید.");
        var invalidate = p.LoginMode != input.LoginMode;
        p.PublicRegistrationEnabled = input.PublicRegistrationEnabled;
        p.RegistrationStartsUtc = input.RegistrationStartsUtc?.ToUniversalTime();
        p.RegistrationEndsUtc = input.RegistrationEndsUtc?.ToUniversalTime();
        p.LoginMode = input.LoginMode;
        p.RecoveryEnabled = input.RecoveryEnabled;
        p.RecoveryChannel = input.RecoveryChannel;
        p.Version++;
        if (invalidate) await repository.InvalidateAllSessionsAsync(cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return true;
    }
}
