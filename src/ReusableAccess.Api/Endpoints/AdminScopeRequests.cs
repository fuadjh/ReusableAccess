namespace ReusableAccess.Api.Endpoints;

// قرارداد ورودی HTTP هستند، نه Commandهای لایهٔ Application.
public sealed record CreateScopeRequest(
    string? Code,
    string? DisplayName,
    Guid? ParentScopeId);

public sealed record UpdateScopeRequest(
    string? DisplayName);