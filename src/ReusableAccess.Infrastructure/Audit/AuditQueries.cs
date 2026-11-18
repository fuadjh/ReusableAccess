using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Audit;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure.Audit;

public sealed class AuditQueries(ApplicationDbContext db) : IAuditQueries
{
    public async Task<PageResult<AuditRow>> GetPageAsync(
        int page,
        CancellationToken cancellationToken)
    {
        page = Math.Max(page, 1);

        var query = db.Set<SecurityAuditEntry>()
            .AsNoTracking();

        var total = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(row => row.OccurredUtc)
            .Skip((page - 1) * 30)
            .Take(30)
            .Select(row => new AuditRow(
                row.Id,
                row.ActorId,
                row.OccurredUtc,
                row.Action,
                row.Entity,
                row.EntityId,
                row.ChangedFields))
            .ToListAsync(cancellationToken);

        return new PageResult<AuditRow>(
            rows,
            total,
            page,
            30);
    }
}