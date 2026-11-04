using Microsoft.AspNetCore.Components;

namespace ReusableAccess.Web.Completion;

public class ConsolePageBase : ComponentBase
{
    [Inject]
    protected ConsoleApi Api { get; set; } = default!;

    protected string? Error;
    protected string? Success;
    protected bool Busy;

    protected async Task Run(
        Func<Task> action, string? success = null)
    {
        if (Busy)
            return;

        Busy = true;
        Error = null;
        Success = null;

        try
        {
            await action();
            Success = success;
        }
        catch (ConsoleApiException e)
        {
            Error = e.Message;
        }
        finally
        {
            Busy = false;
        }
    }
}