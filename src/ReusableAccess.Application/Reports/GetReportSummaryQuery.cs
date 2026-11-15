using MediatR;
using ReusableAccess.Application.Errors;
using ReusableAccess.Application.WorkItems;
using ReusableAccess.Contracts;
namespace ReusableAccess.Application.Reports;
public interface IReportSummaryQueries { Task<ReportSummary> GetAsync(Guid? scopeId, CancellationToken cancellationToken); }
public sealed record GetReportSummaryQuery(string ScopeMode, string? ScopeCode) : IRequest<ReportSummary>, IConcurrencyConflictRequest;
public sealed class GetReportSummaryQueryHandler(IReportSummaryQueries queries, IWorkItemScopeQueries scopes)
    : IRequestHandler<GetReportSummaryQuery, ReportSummary>
{
    public async Task<ReportSummary> Handle(GetReportSummaryQuery request, CancellationToken cancellationToken)
    {
        var scope = await WorkItemScopeSelection.ResolveAsync(request.ScopeMode, request.ScopeCode, scopes, cancellationToken);
        return await queries.GetAsync(scope, cancellationToken);
    }
}
