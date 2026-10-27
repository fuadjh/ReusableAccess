using MediatR;
using Microsoft.AspNetCore.Mvc;
using ReusableAccess.Application.RolePermissions;

namespace ReusableAccess.Api.Endpoints;

public static class AdminRolePermissionsEndpointExtensions
{
    public static IEndpointRouteBuilder MapAdminRolePermissionsEndpoint(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
     "/api/admin/roles/{roleId:guid}/permissions",
     async (
         Guid roleId,
         string? q,
         [FromServices] ISender sender,
         CancellationToken cancellationToken) =>
     {
         var search = string.IsNullOrWhiteSpace(q)
             ? null
             : q.Trim();

         var permissions = await sender.Send(
             new GetRolePermissionsQuery(roleId, search),
             cancellationToken);

         if (permissions is null)
         {
             return Results.NotFound(new
             {
                 message = "نقش پیدا نشد."
             });
         }

         return Results.Ok(new
         {
             data = permissions.Select(permission => new
             {
                 permissionId = permission.PermissionId,
                 key = permission.Key,
                 description = permission.Description,
                 category = permission.Category,
                 isSelected = permission.IsSelected
             }),
             message = "فهرست مجوزهای نقش."
         });
     })
     .RequireAuthorization("PlatformAdmin");

        endpoints.MapPut(
     "/api/admin/roles/{roleId:guid}/permissions",
     async (
         Guid roleId,
         ReplaceRolePermissionsRequest request,
         [FromServices] ISender sender,
         CancellationToken cancellationToken) =>
     {
         var result = await sender.Send(
             new ReplaceRolePermissionsCommand(
                 roleId,
                 request.PermissionKeys),
             cancellationToken);

         if (result.Status
             == RolePermissionsReplacementStatus.InvalidRequest)
         {
             return Results.BadRequest(new
             {
                 message =
                     "permissionKeys باید آرایه‌ای از کلیدهای معتبر باشد."
             });
         }

         if (result.Status
             == RolePermissionsReplacementStatus.RoleNotFound)
         {
             return Results.NotFound(new
             {
                 message = "نقش پیدا نشد."
             });
         }

         if (result.Status
             == RolePermissionsReplacementStatus.UnknownPermissions)
         {
             return Results.BadRequest(new
             {
                 message = "یک یا چند Permission ناشناخته است.",
                 unknownPermissionKeys = result.UnknownPermissionKeys
             });
         }

         return Results.Ok(new
         {
             data = new
             {
                 roleId = result.RoleId,
                 permissionKeys = result.PermissionKeys
             },
             message = "مجوزهای نقش به‌روزرسانی شد."
         });
     })
     .RequireAuthorization("PlatformAdmin");

        return endpoints;
    }
}

public sealed record ReplaceRolePermissionsRequest(
    string[]? PermissionKeys);