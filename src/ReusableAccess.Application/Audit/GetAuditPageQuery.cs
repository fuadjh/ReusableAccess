using MediatR;
using ReusableAccess.Application.Errors;
using ReusableAccess.Contracts;
namespace ReusableAccess.Application.Audit;
public interface IAuditQueries { Task<PageResult<AuditRow>> GetPageAsync(int page, CancellationToken cancellationToken); }
public sealed record GetAuditPageQuery(int Page = 1) : IRequest<PageResult<AuditRow>>, IConcurrencyConflictRequest;
public sealed class GetAuditPageQueryHandler(IAuditQueries queries) : IRequestHandler<GetAuditPageQuery, PageResult<AuditRow>>
{
    public Task<PageResult<AuditRow>> Handle(GetAuditPageQuery request, CancellationToken cancellationToken)
        => queries.GetPageAsync(Math.Max(request.Page, 1), cancellationToken);
}
