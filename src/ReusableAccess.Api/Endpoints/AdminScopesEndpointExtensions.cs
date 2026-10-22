using Microsoft.EntityFrameworkCore;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Api.Endpoints;

public static class AdminScopesEndpointExtensions
{
    public static IEndpointRouteBuilder MapAdminScopesEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/api/admin/scopes",
            async (
                ApplicationDbContext dbContext,
                CancellationToken cancellationToken) =>
            {
                var scopes = await dbContext.Set<AccessScope>()
                    .AsNoTracking()
                    .OrderBy(scope => scope.Code)
                    .Select(scope => new
                    {
                        scopeId = scope.Id,
                        scopeCode = scope.Code,
                        displayName = scope.DisplayName,
                        parentScopeId = scope.ParentScopeId
                    })
                    .ToListAsync(cancellationToken);

                return Results.Ok(new
                {
                    data = scopes,
                    message = "فهرست Scopeها."
                });
            })
            .RequireAuthorization("PlatformAdmin");

        endpoints.MapPost(
            "/api/admin/scopes",
            async (
                CreateScopeRequest request,
                ApplicationDbContext dbContext,
                CancellationToken cancellationToken) =>
            {
                var code = request.Code?.Trim();
                var displayName = request.DisplayName?.Trim();

                if (string.IsNullOrWhiteSpace(code)
                    || code.Length > 100
                    || string.IsNullOrWhiteSpace(displayName)
                    || displayName.Length > 200
                    || request.ParentScopeId == Guid.Empty)
                {
                    return Results.BadRequest(new
                    {
                        message =
                            "کد و نام نمایشی الزامی‌اند؛ طول کد حداکثر ۱۰۰ و نام حداکثر ۲۰۰ کاراکتر است."
                    });
                }

                var codeExists = await dbContext.Set<AccessScope>()
                    .AnyAsync(scope => scope.Code == code, cancellationToken);

                if (codeExists)
                {
                    return Results.Conflict(new
                    {
                        message = "این کد Scope قبلاً ثبت شده است."
                    });
                }

                if (request.ParentScopeId is Guid parentId)
                {
                    var parentExists = await dbContext.Set<AccessScope>()
                        .AnyAsync(
                            scope => scope.Id == parentId,
                            cancellationToken);

                    if (!parentExists)
                    {
                        return Results.BadRequest(new
                        {
                            message = "Scope والد پیدا نشد."
                        });
                    }
                }

                var scope = new AccessScope(
                    code,
                    displayName,
                    request.ParentScopeId);

                dbContext.Set<AccessScope>().Add(scope);
                await dbContext.SaveChangesAsync(cancellationToken);

                return Results.Created(
                    $"/api/admin/scopes/{scope.Id}",
                    new
                    {
                        data = new
                        {
                            scopeId = scope.Id,
                            scopeCode = scope.Code,
                            displayName = scope.DisplayName,
                            parentScopeId = scope.ParentScopeId
                        },
                        message = "Scope ایجاد شد."
                    });
            })
            .RequireAuthorization("PlatformAdmin");

        endpoints.MapPut(
            "/api/admin/scopes/{scopeId:guid}",
            async (
                Guid scopeId,
                UpdateScopeRequest request,
                ApplicationDbContext dbContext,
                CancellationToken cancellationToken) =>
            {
                var displayName = request.DisplayName?.Trim();

                if (string.IsNullOrWhiteSpace(displayName)
                    || displayName.Length > 200)
                {
                    return Results.BadRequest(new
                    {
                        message =
                            "نام نمایشی Scope الزامی است و حداکثر ۲۰۰ کاراکتر دارد."
                    });
                }

                var scope = await dbContext.Set<AccessScope>()
                    .SingleOrDefaultAsync(
                        item => item.Id == scopeId,
                        cancellationToken);

                if (scope is null)
                {
                    return Results.NotFound(new
                    {
                        message = "Scope پیدا نشد."
                    });
                }

                scope.Rename(displayName);
                await dbContext.SaveChangesAsync(cancellationToken);

                return Results.Ok(new
                {
                    data = new
                    {
                        scopeId = scope.Id,
                        scopeCode = scope.Code,
                        displayName = scope.DisplayName,
                        parentScopeId = scope.ParentScopeId
                    },
                    message = "Scope به‌روزرسانی شد."
                });
            })
            .RequireAuthorization("PlatformAdmin");

        return endpoints;
    }
}

public sealed record CreateScopeRequest(
    string? Code,
    string? DisplayName,
    Guid? ParentScopeId);

public sealed record UpdateScopeRequest(
    string? DisplayName);