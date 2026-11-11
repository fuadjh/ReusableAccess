using System.ComponentModel.DataAnnotations;
using ReusableAccess.Contracts;
using ReusableAccess.Web.Completion;

namespace ReusableAccess.Web.Ui;

public sealed class ManagedUsersService(ConsoleApi api)
{
    private const string Root = "api/admin/users";

    public Task<UserRow> MeAsync()
        => api.GetAsync<UserRow>("api/account/me");

    public Task<PageResult<UserRow>> ListAsync(string? query, int page)
        => api.GetAsync<PageResult<UserRow>>(
            $"{Root}/manage?q={Uri.EscapeDataString(query ?? "")}&page={page}");

    public Task<UserRow> GetAsync(Guid id)
        => api.GetAsync<UserRow>($"{Root}/{id:D}/details");

    public Task<Guid> CreateAsync(CreateUserInput input)
    {
        Validator.ValidateObject(
            input, new ValidationContext(input), true);

        return api.PostAsync<Guid>(Root, input);
    }

    public Task UpdateAsync(Guid id, UpdateUserInput input)
    {
        Validator.ValidateObject(
            input, new ValidationContext(input), true);

        return api.WriteAsync(
            HttpMethod.Put, $"{Root}/{id:D}", input);
    }

    public Task SetActiveAsync(UserRow user)
        => api.WriteAsync(
            HttpMethod.Put,
            $"{Root}/{user.UserId:D}/active",
            new ActiveUserInput(
                !user.IsActive, user.ConcurrencyStamp));

    public Task ResetPasswordAsync(Guid id, string password)
        => api.WriteAsync(
            HttpMethod.Post,
            $"{Root}/{id:D}/reset-password",
            new AdminPasswordInput(password));
}