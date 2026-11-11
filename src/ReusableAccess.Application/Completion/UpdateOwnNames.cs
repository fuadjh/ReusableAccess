using System.ComponentModel.DataAnnotations;
using MediatR;
using ReusableAccess.Application.Users;
using ReusableAccess.Contracts;

namespace ReusableAccess.Application.Completion;

public sealed record UpdateOwnNames(
    Guid UserId,
    UpdateOwnNamesInput Input) : IRequest<bool>;

public sealed class UpdateOwnNamesHandler(
    IManagedUserReadStore store,
    ISender sender) : IRequestHandler<UpdateOwnNames, bool>
{
    public async Task<bool> Handle(
        UpdateOwnNames request,
        CancellationToken ct)
    {
        var errors = new List<ValidationResult>();

        if (!Validator.TryValidateObject(
                request.Input,
                new ValidationContext(request.Input),
                errors,
                true))
        {
            throw new CompletionException(
                400,
                string.Join(" ", errors.Select(x => x.ErrorMessage)));
        }

        var current = await store.CurrentUserAsync(
            request.UserId,
            ct);

        // فقط نام و نام خانوادگی از درخواست کاربر دریافت می‌شود.
        // سایر مشخصات از وضعیت معتبر سمت سرور خوانده می‌شوند.
        var input = new UpdateUserInput
        {
            FirstName = request.Input.FirstName.Trim(),
            LastName = request.Input.LastName.Trim(),
            NationalId = current.NationalId,
            OrganizationalCode = current.OrganizationalCode,
            Email = current.Email,
            Mobile = current.Mobile,

            // نسخه‌ای که کاربر هنگام مشاهده فرم داشته است حفظ می‌شود.
            // استفاده از current.ConcurrencyStamp می‌تواند تعارض را پنهان کند.
            ConcurrencyStamp = request.Input.ConcurrencyStamp
        };

        // قواعد و کنترل هم‌زمانی از مسیر Command کاربران اجرا می‌شوند.
        return await sender.Send(
            new UpdateManagedUser(
                request.UserId,
                request.UserId,
                input),
            ct);
    }
}