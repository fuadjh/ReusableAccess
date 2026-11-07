using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using ReusableAccess.Web.Completion;

namespace ReusableAccess.Web.Ui;

public class UiPageBase : ComponentBase, IDisposable
{
    [Inject] protected ConsoleApi Api { get; set; } = default!;
    [Inject] protected ISnackbar Snackbar { get; set; } = default!;
    [Inject] private ILogger<UiPageBase> Logger { get; set; } = default!;

    protected string? Error;
    protected bool Busy, NeedsLogin, Forbidden;
    protected bool IsDisposed { get; private set; }
    protected bool Unavailable => Busy || NeedsLogin || Forbidden;

    protected async Task<bool> Run(
        Func<Task> action,
        string? success = null,
        bool writing = false,
        Func<bool>? current = null)
    {
        if (Busy || IsDisposed)
            return false;

        bool Current() => !IsDisposed && (current?.Invoke() ?? true);

        Busy = true;
        Error = null;
        await InvokeAsync(StateHasChanged);
        var completed = false;

        try
        {
            await action();
            completed = Current();
            if (completed && !string.IsNullOrWhiteSpace(success))
                Snackbar.Add(success, Severity.Success);
        }
        catch (ValidationException error)
        {
            if (Current())
                Report(error.Message, Severity.Warning);
        }
        catch (ConsoleApiException error)
        {
            if (Current())
            {
                NeedsLogin = error.StatusCode == 401;
                Forbidden = error.StatusCode == 403;
                Report(
                    writing && error.StatusCode >= 500
                        ? "نتیجهٔ ثبت قطعی نیست؛ ابتدا فهرست را تازه‌سازی و بررسی کنید."
                        : error.Message,
                    Severity.Error);
            }
        }
        catch (Exception error)
        {
            // متن Exception ممکن است اطلاعات حساس داشته باشد.
            Logger.LogError("UI operation failed ({ErrorType}).",
                error.GetType().Name);

            if (Current())
                Report(
                    writing
                        ? "نتیجهٔ عملیات مشخص نیست؛ پیش از ثبت دوباره، فهرست را بررسی کنید."
                        : "دریافت اطلاعات انجام نشد؛ دوباره تلاش کنید.",
                    Severity.Error);
        }
        finally
        {
            Busy = false;
            if (!IsDisposed)
                await InvokeAsync(StateHasChanged);
        }

        return completed;
    }

    protected async Task ReloadAfterSave(Func<Task> load)
    {
        try
        {
            await load();
        }
        catch (Exception error)
        {
            Logger.LogWarning("Refresh after save failed ({ErrorType}).",
                error.GetType().Name);

            if (IsDisposed)
                return;

            if (error is ConsoleApiException apiError)
            {
                NeedsLogin = apiError.StatusCode == 401;
                Forbidden = apiError.StatusCode == 403;
            }

            Report(
                "ذخیره انجام شد، اما فهرست تازه نشد. ثبت را تکرار نکنید؛ فهرست را تازه‌سازی کنید.",
                Severity.Warning);
        }
    }

    private void Report(string message, Severity severity)
    {
        Error = message;
        Snackbar.Add(message, severity);
    }

    public virtual void Dispose() => IsDisposed = true;
}