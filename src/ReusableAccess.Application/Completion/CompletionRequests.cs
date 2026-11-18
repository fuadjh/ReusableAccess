using MediatR;
using ReusableAccess.Contracts;

namespace ReusableAccess.Application.Completion;






public sealed record GetAuthSettings : IRequest<SettingsView>;

public sealed record SaveAuthSettings(
    Guid ActorId, AuthSettingsInput Input) : IRequest<bool>;

public sealed record DeleteAccessEntity(
    Guid ActorId, string Kind, Guid Id) : IRequest<bool>;

public sealed record ReadAudit(int Page = 1)
    : IRequest<PageResult<AuditRow>>;



public sealed record ChangeOwnPassword(
    Guid UserId, ChangePasswordInput Input) : IRequest<bool>;

public sealed record ReadBundles(Guid UserId)
    : IRequest<IReadOnlyList<BundleRow>>;

public sealed record SaveSimpleAssignment(
    Guid ActorId, Guid UserId, Guid RoleId, Guid ScopeId)
    : IRequest<Guid>;

public sealed record WriteWorkItem(
    Guid? Id, string ScopeMode, string? ScopeCode, WorkItemInput Input)
    : IRequest<Guid>;

public sealed record RemoveWorkItem(
    Guid Id, string ScopeMode, string? ScopeCode) : IRequest<bool>;

public sealed record ReadConsoleWorkItems(
    string ScopeMode, string? ScopeCode)
    : IRequest<IReadOnlyList<WorkItemRow>>;

public sealed record ReadWritableScopes(
    string ScopeMode, string? ScopeCode)
    : IRequest<IReadOnlyList<ScopeRow>>;

public sealed record ReadReport(
    string ScopeMode, string? ScopeCode) : IRequest<ReportSummary>;

public interface ICompletionStore
{



    Task<bool> ChangePasswordAsync(
        Guid id, ChangePasswordInput input, CancellationToken ct);

    Task<SettingsView> SettingsAsync(CancellationToken ct);

    Task<bool> SaveSettingsAsync(
        Guid actor, AuthSettingsInput input, CancellationToken ct);

    Task<bool> DeleteAsync(string kind, Guid id, CancellationToken ct);
    Task<PageResult<AuditRow>> AuditAsync(int page, CancellationToken ct);

    Task<IReadOnlyList<BundleRow>> BundlesAsync(
        Guid userId, CancellationToken ct);

    Task<Guid> AssignAsync(
        Guid userId, Guid roleId, Guid scopeId, CancellationToken ct);

    Task<Guid> WriteWorkItemAsync(
        Guid? id, string mode, string? scope,
        WorkItemInput input, CancellationToken ct);

    Task<bool> DeleteWorkItemAsync(
        Guid id, string mode, string? scope, CancellationToken ct);

    Task<IReadOnlyList<WorkItemRow>> ListWorkItemsAsync(
        string mode, string? scope, CancellationToken ct);

    Task<IReadOnlyList<ScopeRow>> WritableScopesAsync(
        string mode, string? scope, CancellationToken ct);

    Task<ReportSummary> ReportAsync(
        string mode, string? scope, CancellationToken ct);
}

// Requestها مستقل‌اند؛ صرفاً handlerهای مرتبط را در یک کلاس جمع کرده‌ایم.
public sealed class CompletionHandlers(ICompletionStore store) :

    IRequestHandler<GetAuthSettings, SettingsView>,
    IRequestHandler<SaveAuthSettings, bool>,
    IRequestHandler<DeleteAccessEntity, bool>,
    IRequestHandler<ReadAudit, PageResult<AuditRow>>,

    IRequestHandler<ChangeOwnPassword, bool>,
    IRequestHandler<ReadBundles, IReadOnlyList<BundleRow>>,
    IRequestHandler<SaveSimpleAssignment, Guid>,
    IRequestHandler<WriteWorkItem, Guid>,
    IRequestHandler<RemoveWorkItem, bool>,
    IRequestHandler<ReadConsoleWorkItems, IReadOnlyList<WorkItemRow>>,
    IRequestHandler<ReadWritableScopes, IReadOnlyList<ScopeRow>>,
    IRequestHandler<ReadReport, ReportSummary>
{


    public Task<SettingsView> Handle(GetAuthSettings r, CancellationToken ct)
        => store.SettingsAsync(ct);

    public Task<bool> Handle(SaveAuthSettings r, CancellationToken ct)
        => store.SaveSettingsAsync(r.ActorId, r.Input, ct);

    public Task<bool> Handle(DeleteAccessEntity r, CancellationToken ct)
        => store.DeleteAsync(r.Kind, r.Id, ct);

    public Task<PageResult<AuditRow>> Handle(ReadAudit r, CancellationToken ct)
        => store.AuditAsync(r.Page, ct);



    public Task<bool> Handle(ChangeOwnPassword r, CancellationToken ct)
        => store.ChangePasswordAsync(r.UserId, r.Input, ct);

    public Task<IReadOnlyList<BundleRow>> Handle(ReadBundles r, CancellationToken ct)
        => store.BundlesAsync(r.UserId, ct);

    public Task<Guid> Handle(SaveSimpleAssignment r, CancellationToken ct)
        => store.AssignAsync(r.UserId, r.RoleId, r.ScopeId, ct);

    public Task<Guid> Handle(WriteWorkItem r, CancellationToken ct)
        => store.WriteWorkItemAsync(
            r.Id, r.ScopeMode, r.ScopeCode, r.Input, ct);

    public Task<bool> Handle(RemoveWorkItem r, CancellationToken ct)
        => store.DeleteWorkItemAsync(r.Id, r.ScopeMode, r.ScopeCode, ct);

    public Task<IReadOnlyList<WorkItemRow>> Handle(
        ReadConsoleWorkItems r, CancellationToken ct)
        => store.ListWorkItemsAsync(r.ScopeMode, r.ScopeCode, ct);

    public Task<IReadOnlyList<ScopeRow>> Handle(
        ReadWritableScopes r, CancellationToken ct)
        => store.WritableScopesAsync(r.ScopeMode, r.ScopeCode, ct);

    public Task<ReportSummary> Handle(ReadReport r, CancellationToken ct)
        => store.ReportAsync(r.ScopeMode, r.ScopeCode, ct);
}



