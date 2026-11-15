using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.WorkItems;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Persistence;
namespace ReusableAccess.Infrastructure.WorkItems;
public sealed class WorkItemRepository(ApplicationDbContext db) : IWorkItemRepository
{
    public Task<WorkItem?> FindForUpdateAsync(Guid id, Guid? allowedScopeId, CancellationToken cancellationToken)
        => db.Set<WorkItem>().AsTracking().SingleOrDefaultAsync(x => x.Id == id &&
            (!allowedScopeId.HasValue || x.ScopeId == allowedScopeId.Value), cancellationToken);
    public void Add(WorkItem item) => db.Add(item);
    public void Remove(WorkItem item) => db.Remove(item);
    public async Task SaveChangesAsync(CancellationToken cancellationToken) => _ = await db.SaveChangesAsync(cancellationToken);
}
