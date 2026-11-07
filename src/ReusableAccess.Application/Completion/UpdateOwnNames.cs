using System.ComponentModel.DataAnnotations;
using MediatR;
using ReusableAccess.Contracts;

namespace ReusableAccess.Application.Completion;

public sealed record UpdateOwnNames(
    Guid UserId, UpdateOwnNamesInput Input) : IRequest<bool>;

public sealed class UpdateOwnNamesHandler(
    ICompletionStore store,
    ISender sender) : IRequestHandler<UpdateOwnNames, bool>
{
    public async Task<bool> Handle(UpdateOwnNames request, CancellationToken ct)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(
                request.Input,
                new ValidationContext(request.Input),
                errors, true))
        {
            throw new CompletionException(400,
                string.Join(" ", errors.Select(x => x.ErrorMessage)));
        }

        var current = await store.CurrentUserAsync(request.UserId, ct);

        // اطلاعات حفاظت‌شده از سرور می‌آید، نه از بدنهٔ درخواست کاربر.
        var input = new UpdateUserInput
        {
            FirstName = request.Input.FirstName.Trim(),
            LastName = request.Input.LastName.Trim(),
            NationalId = current.NationalId,
            OrganizationalCode = current.OrganizationalCode,
            Email = current.Email,
            Mobile = current.Mobile,
            ConcurrencyStamp = request.Input.ConcurrencyStamp
        };

        // مسیر قواعد موجود، اعتبارسنجی و کنترل هم‌زمانی حفظ می‌شود.
        return await sender.Send(
            new UpdateManagedUser(request.UserId, request.UserId, input), ct);
    }
}