using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using MediatR;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Identity;

namespace ReusableAccess.Application.Completion;

public sealed class CompletionException(
    int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public static class CompletionValidation
{
    public static void Profile(UserProfileInput input)
    {
        var errors = new List<ValidationResult>();

        if (!Validator.TryValidateObject(
                input, new ValidationContext(input), errors, true))
        {
            throw new CompletionException(
                400, string.Join(" ", errors.Select(x => x.ErrorMessage)));
        }

        if ($"{input.FirstName.Trim()} {input.LastName.Trim()}".Length > 200)
            throw new CompletionException(400, "مجموع نام و نام خانوادگی بیش از حد مجاز است.");

        var mobile = IdentityRules.NormalizeMobile(input.Mobile);

        if (!Regex.IsMatch(mobile, @"^\+[1-9][0-9]{7,14}$"))
            throw new CompletionException(400, "شماره موبایل معتبر نیست.");

        if (string.IsNullOrWhiteSpace(input.FirstName) ||
            string.IsNullOrWhiteSpace(input.LastName) ||
            string.IsNullOrWhiteSpace(input.OrganizationalCode))
        {
            throw new CompletionException(400, "همهٔ مشخصات الزامی‌اند.");
        }
    }
}






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

public interface IAccountSessionValidator
{
    Task<bool> IsCurrentAsync(
        Guid userId, int sessionVersion, CancellationToken ct);
}

public interface IAccountWorkflow
{
    Task<PublicAuthOptions> OptionsAsync(CancellationToken ct);

    Task<LoginOutcome> PasswordAsync(
        string nationalId, string password, bool remember, CancellationToken ct);

    Task<LoginOutcome> VerifyLoginAsync(
        Guid challenge, string code, CancellationToken ct);

    Task<LoginOutcome> ChangeRequiredPasswordAsync(
        Guid challenge, string password, CancellationToken ct);

    Task RegisterAsync(CreateUserInput input, CancellationToken ct);

    Task<Guid> RequestRecoveryAsync(string nationalId, CancellationToken ct);

    Task ResetRecoveredPasswordAsync(
        Guid challenge, string code, string password, CancellationToken ct);

    Task<Guid> RequestContactAsync(
        Guid userId, DeliveryChannel channel, CancellationToken ct);

    Task ConfirmContactAsync(
        Guid userId, Guid challenge, string code, CancellationToken ct);

    Task SignOutAsync();
}

public interface IAccountMessageSender
{
    bool IsConfigured(DeliveryChannel channel);

    Task SendAsync(
        DeliveryChannel channel, string destination,
        string code, CancellationToken ct);
}

public interface IRecoveryCodeQueue
{
    bool TryEnqueue(
        DeliveryChannel channel, string destination, string code);
}