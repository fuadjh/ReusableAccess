using ReusableAccess.Contracts;

namespace ReusableAccess.Application.Users;

// عملیات مدیریتی، بدون دسترسی به تنظیمات ورود، Audit یا WorkItem.
public interface IManagedUserCommandStore
{
    Task<Guid> CreateManagedAsync(
        CreateUserInput input,
        CancellationToken cancellationToken);

    Task<bool> UpdateUserAsync(
        Guid actorId,
        Guid userId,
        UpdateUserInput input,
        CancellationToken cancellationToken);

    Task<bool> SetActiveAsync(
        Guid actorId,
        Guid userId,
        ActiveUserInput input,
        CancellationToken cancellationToken);

    Task<bool> ResetPasswordAsync(
        Guid actorId,
        Guid userId,
        string password,
        CancellationToken cancellationToken);
}

// ثبت‌نام عمومی نباید به عملیات مدیریتی حساب وابسته باشد.
public interface IUserRegistrationStore
{
    Task<Guid> RegisterAsync(
        CreateUserInput input,
        CancellationToken cancellationToken);
}
