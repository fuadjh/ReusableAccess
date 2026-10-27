using MediatR;
using Microsoft.AspNetCore.Mvc;

using ReusableAccess.Application.Bundles;
using ReusableAccess.Application.Users;


namespace ReusableAccess.Api.Endpoints;

public static class AdminBundlesEndpointExtensions
{
    public static IEndpointRouteBuilder MapAdminBundlesEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
     "/api/admin/users",
     async (
         string? q,
         [FromServices] ISender sender,
         CancellationToken cancellationToken) =>
     {
         var search = q?.Trim();

         if (string.IsNullOrWhiteSpace(search) || search.Length < 2)
         {
             return Results.BadRequest(new
             {
                 message = "برای جست‌وجوی کاربر حداقل دو حرف وارد کنید."
             });
         }

         var users = await sender.Send(
             new SearchUsersQuery(search),
             cancellationToken);

         return Results.Ok(new
         {
             data = users.Select(user => new
             {
                 userId = user.UserId,
                 userName = user.UserName,
                 displayName = user.DisplayName
             }),
             message = "کاربران پیدا‌شده."
         });
     })
     .RequireAuthorization("PlatformAdmin");

        endpoints.MapGet(
     "/api/admin/users/{userId:guid}/bundles",
     async (
         Guid userId,
         [FromServices] ISender sender,
         CancellationToken cancellationToken) =>
     {
         var bundles = await sender.Send(
             new GetUserBundlesQuery(userId),
             cancellationToken);

         if (bundles is null)
         {
             return Results.NotFound(new
             {
                 message = "کاربر پیدا نشد."
             });
         }

         return Results.Ok(new
         {
             data = bundles.Select(bundle => new
             {
                 bundleId = bundle.BundleId,
                 bundle.Name,
                 bundle.SortOrder,
                 bundle.IsEnabled,
                 scopeMode = bundle.ScopeMode,
                 roleIds = bundle.RoleIds,
                 scopeIds = bundle.ScopeIds
             }),
             message = "Bundleهای کاربر."
         });
     })
     .RequireAuthorization("PlatformAdmin");

        endpoints.MapPost(
     "/api/admin/users/{userId:guid}/bundles",
     async (
         Guid userId,
         SaveBundleRequest request,
         [FromServices] ISender sender,
         CancellationToken cancellationToken) =>
     {
         var result = await sender.Send(
             new CreateUserBundleCommand(
                 userId,
                 request.Name,
                 request.RoleIds,
                 request.ScopeMode,
                 request.ScopeIds,
                 request.SortOrder,
                 request.IsEnabled),
             cancellationToken);

         if (result.Status == BundleCreationStatus.UserNotFound)
         {
             return Results.NotFound(new
             {
                 message = "کاربر پیدا نشد."
             });
         }

         if (result.Status == BundleCreationStatus.InvalidRequest)
         {
             return Results.BadRequest(new
             {
                 message = result.ErrorMessage
             });
         }

         return Results.Created(
             $"/api/admin/bundles/{result.BundleId}",
             new
             {
                 data = new { bundleId = result.BundleId },
                 message = "Bundle ایجاد شد."
             });
     })
     .RequireAuthorization("PlatformAdmin");

        endpoints.MapPut(
    "/api/admin/bundles/{bundleId:guid}",
    async (
        Guid bundleId,
        SaveBundleRequest request,
        [FromServices] ISender sender,
        CancellationToken cancellationToken) =>
    {
        var result = await sender.Send(
            new UpdateUserBundleCommand(
                bundleId,
                request.Name,
                request.RoleIds,
                request.ScopeMode,
                request.ScopeIds,
                request.SortOrder,
                request.IsEnabled),
            cancellationToken);

        if (result.Status == BundleUpdateStatus.BundleNotFound)
        {
            return Results.NotFound(new
            {
                message = "Bundle پیدا نشد."
            });
        }

        if (result.Status == BundleUpdateStatus.InvalidRequest)
        {
            return Results.BadRequest(new
            {
                message = result.ErrorMessage
            });
        }

        return Results.Ok(new
        {
            data = new { bundleId },
            message = "Bundle به‌روزرسانی شد."
        });
    })
    .RequireAuthorization("PlatformAdmin");

        return endpoints;
    }

   
}

public sealed record SaveBundleRequest(
    string? Name,
    Guid[]? RoleIds,
    string? ScopeMode,
    Guid[]? ScopeIds,
    int SortOrder,
    bool IsEnabled);