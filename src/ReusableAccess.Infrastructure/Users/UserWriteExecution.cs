using ReusableAccess.Application.Accounts;
namespace ReusableAccess.Infrastructure.Users;
public sealed class UserWriteExecution : IUserWriteExecution
{
    public Task<T> RunAsync<T>(Func<Task<T>> action) => UserWriteConflict.Run(action);
}
