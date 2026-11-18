using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.WorkItems;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure.WorkItems;

public sealed class WorkItemListQueries(ApplicationDbContext db)
    : IWorkItemListQueries
{
    public async Task<IReadOnlyList<WorkItemRow>> ListAsync(
        Guid? scopeId,
        CancellationToken cancellationToken)
    {
        // این مسیر فقط خواندنی است؛ موجودیت‌ها Track نمی‌شوند.
        var query = db.Set<WorkItem>()
            .AsNoTracking();

        // انتخاب و اعتبارسنجی محدوده قبلاً در Application انجام شده است.
        // null مطابق رفتار قبلی یعنی اعمال‌نکردن فیلتر محدوده.
        if (scopeId.HasValue)
        {
            query = query.Where(
                item => item.ScopeId == scopeId.Value);
        }

        // Projection در دیتابیس انجام می‌شود.
        return await query
            .OrderBy(item => item.Title)
            .Select(item => new WorkItemRow(
                item.Id,
                item.Title,
                item.Status))
            .ToListAsync(cancellationToken);
    }
}