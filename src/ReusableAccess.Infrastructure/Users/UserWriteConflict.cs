using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Completion;

namespace ReusableAccess.Infrastructure.Users;

internal static class UserWriteConflict
{
    public static void Check(IdentityResult result)
    {
        if (result.Succeeded)
            return;

        var errors = result.Errors.ToArray();

        if (errors.Any(error => error.Code == "ConcurrencyFailure"))
        {
            throw new CompletionException(
                409, "اطلاعات هم‌زمان تغییر کرده است؛ دوباره دریافت کنید.");
        }

        if (errors.Any(error =>
                error.Code is "DuplicateUserName" or "DuplicateEmail"))
        {
            throw new CompletionException(
                409, "کد ملی یا ایمیل قبلاً ثبت شده است.");
        }

        throw new CompletionException(
            400,
            string.Join(" ", errors.Select(error => error.Description)));
    }

    public static async Task<T> Run<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new CompletionException(
                409, "اطلاعات هم‌زمان تغییر کرده است؛ دوباره دریافت کنید.");
        }
        catch (Exception error) when (HasSqlError(error, 2601, 2627))
        {
            throw new CompletionException(
                409, "اطلاعات تکراری است؛ فهرست را تازه‌سازی کنید.");
        }
        catch (Exception error) when (HasSqlError(error, 1205))
        {
            // retry خودکار write انجام نمی‌دهیم؛ تراکنش قربانی deadlock برگشت خورده است.
            throw new CompletionException(
                409, "عملیات هم‌زمان دیگری انجام شد؛ اطلاعات را دوباره دریافت کنید.");
        }
    }

    private static bool HasSqlError(Exception error, params int[] numbers)
    {
        for (Exception? current = error;
             current is not null;
             current = current.InnerException)
        {
            if (current is SqlException sql &&
                sql.Errors.Cast<SqlError>()
                    .Any(item => numbers.Contains(item.Number)))
            {
                return true;
            }
        }

        return false;
    }
}