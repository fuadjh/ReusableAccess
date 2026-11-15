using MediatR;
using ReusableAccess.Application.Errors;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Entities;
namespace ReusableAccess.Application.WorkItems;
public interface IWorkItemRepository
{
    Task<WorkItem?> FindForUpdateAsync(Guid id, Guid? allowedScopeId, CancellationToken cancellationToken);
    void Add(WorkItem item);
    void Remove(WorkItem item);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
public sealed record SaveWorkItemCommand(Guid? Id, string ScopeMode, string? ScopeCode, WorkItemInput Input)
    : IRequest<Guid>, IConcurrencyConflictRequest;
public sealed class SaveWorkItemCommandHandler(IWorkItemRepository repository, IWorkItemScopeQueries scopes)
    : IRequestHandler<SaveWorkItemCommand, Guid>
{
    public async Task<Guid> Handle(SaveWorkItemCommand request, CancellationToken cancellationToken)
    {
        // resolution محدوده عمداً پیش از اعتبارسنجی payload باقی می‌ماند.
        var allowed = await WorkItemScopeSelection.ResolveAsync(request.ScopeMode, request.ScopeCode, scopes, cancellationToken);
        var input = request.Input;
        if (string.IsNullOrWhiteSpace(input.Title) || input.Title.Trim().Length > 200 || input.Status is not ("Open" or "Closed"))
            throw new UseCaseException(400, "عنوان یا وضعیت معتبر نیست.");
        WorkItem item;
        if (request.Id.HasValue)
        {
            item = await repository.FindForUpdateAsync(request.Id.Value, allowed, cancellationToken)
                ?? throw new UseCaseException(404, "مورد کاری پیدا نشد.");
            if (input.ScopeId != Guid.Empty && input.ScopeId != item.ScopeId)
                throw new UseCaseException(400, "انتقال مورد کاری بین محدوده‌ها مجاز نیست.");
            item.Update(input.Title, input.Status);
        }
        else
        {
            var target = allowed ?? input.ScopeId;
            if (allowed.HasValue && input.ScopeId != Guid.Empty && input.ScopeId != allowed.Value)
                throw new UseCaseException(403, "محدوده خارج از پروفایل فعال است.");
            if (!await scopes.ExistsAsync(target, cancellationToken))
                throw new UseCaseException(400, "محدودهٔ مقصد معتبر نیست.");
            item = new WorkItem(target, input.Title, input.Status);
            repository.Add(item);
        }
        await repository.SaveChangesAsync(cancellationToken);
        return item.Id;
    }
}
public sealed record DeleteWorkItemCommand(Guid Id, string ScopeMode, string? ScopeCode)
    : IRequest<bool>, IConcurrencyConflictRequest;
public sealed class DeleteWorkItemCommandHandler(IWorkItemRepository repository, IWorkItemScopeQueries scopes)
    : IRequestHandler<DeleteWorkItemCommand, bool>
{
    public async Task<bool> Handle(DeleteWorkItemCommand request, CancellationToken cancellationToken)
    {
        var allowed = await WorkItemScopeSelection.ResolveAsync(request.ScopeMode, request.ScopeCode, scopes, cancellationToken);
        var item = await repository.FindForUpdateAsync(request.Id, allowed, cancellationToken)
            ?? throw new UseCaseException(404, "مورد کاری پیدا نشد.");
        repository.Remove(item);
        await repository.SaveChangesAsync(cancellationToken);
        return true;
    }
}
