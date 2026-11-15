using ReusableAccess.Application.Errors;
using ReusableAccess.Contracts;
namespace ReusableAccess.Application.WorkItems;
public interface IWorkItemScopeQueries
{
    Task<Guid?> FindIdByCodeAsync(string? code, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(Guid scopeId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ScopeRow>> ListAsync(Guid? scopeId, CancellationToken cancellationToken);
}
public static class WorkItemScopeSelection
{
    // تفسیر ورودی در Application است؛ Infrastructure فقط فیلتر Guid را اجرا می‌کند.
    public static async Task<Guid?> ResolveAsync(string mode, string? code, IWorkItemScopeQueries scopes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.Equals(mode, "all", StringComparison.OrdinalIgnoreCase)) return null;
        if (!string.IsNullOrWhiteSpace(mode) && !string.Equals(mode, "selected", StringComparison.OrdinalIgnoreCase))
            throw new UseCaseException(400, "حالت محدوده معتبر نیست.");
        return await scopes.FindIdByCodeAsync(code, cancellationToken)
            ?? throw new UseCaseException(404, "محدوده پیدا نشد.");
    }
}
