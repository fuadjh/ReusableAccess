using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Reports;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure.Reports;

public sealed class ReportSummaryQueries(ApplicationDbContext db)
    : IReportSummaryQueries
{
    public async Task<ReportSummary> GetAsync(
        Guid? scopeId,
        CancellationToken cancellationToken)
    {
        var query = db.Set<WorkItem>()
            .AsNoTracking();

        if (scopeId.HasValue)
        {
            query = query.Where(
                item => item.ScopeId == scopeId.Value);
        }

        return new ReportSummary(
            await query.CountAsync(cancellationToken),

            await query.CountAsync(
                item => item.Status == "Open",
                cancellationToken),

            await query.CountAsync(
                item => item.Status == "Closed",
                cancellationToken));
    }
}