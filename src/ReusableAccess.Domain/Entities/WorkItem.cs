namespace ReusableAccess.Domain.Entities;

public sealed class WorkItem
{
    private WorkItem()
    {
    }

    public WorkItem(Guid scopeId, string title, string status)
    {
        if (scopeId == Guid.Empty)
        {
            throw new ArgumentException(
                "شناسهٔ محدوده نباید خالی باشد.",
                nameof(scopeId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        Id = Guid.NewGuid();
        ScopeId = scopeId;
        Title = title.Trim();
        Status = status.Trim();
    }
    public void Update(string title, string status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        if (title.Trim().Length > 200 ||
            status is not ("Open" or "Closed"))
        {
            throw new ArgumentException("عنوان یا وضعیت معتبر نیست.");
        }

        Title = title.Trim();
        Status = status;
    }
    public Guid Id { get; private set; }
    public Guid ScopeId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
}