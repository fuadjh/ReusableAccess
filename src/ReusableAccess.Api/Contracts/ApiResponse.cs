namespace ReusableAccess.Api.Contracts;

public sealed record ApiResponse<T>(
    T Data,
    string Message);