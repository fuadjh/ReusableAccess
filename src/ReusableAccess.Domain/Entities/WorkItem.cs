namespace ReusableAccess.Domain.Entities;
public sealed class WorkItem
{
    private WorkItem() { }
    public WorkItem(Guid scopeId, string title, string status)
    {
        if (scopeId == Guid.Empty) throw new ArgumentException("شناسهٔ محدوده نباید خالی باشد.", nameof(scopeId));
        var normalized = Validate(title, status);
        Id = Guid.NewGuid(); ScopeId = scopeId; Title = normalized; Status = status;
    }
    public Guid Id { get; private set; }
    public Guid ScopeId { get; private set; }
    public string Title { get; private set; } = "";
    public string Status { get; private set; } = "";
    public void Update(string title, string status)
    {
        var normalized = Validate(title, status);
        Title = normalized; Status = status;
    }
    private static string Validate(string title, string status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        var result = title.Trim();
        if (result.Length > 200 || status is not ("Open" or "Closed")) throw new ArgumentException("عنوان یا وضعیت معتبر نیست.");
        return result;
    }
}
