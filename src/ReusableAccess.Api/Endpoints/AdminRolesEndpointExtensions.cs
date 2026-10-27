using MediatR;
using Microsoft.AspNetCore.Mvc;
using ReusableAccess.Application.Roles;


namespace ReusableAccess.Api.Endpoints;

public static class AdminRolesEndpointExtensions
{
    public static IEndpointRouteBuilder MapAdminRolesEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
     "/api/admin/roles",
     async (
         [FromServices] ISender sender,
         CancellationToken cancellationToken) =>
     {
         var roles = await sender.Send(
             new GetRolesQuery(),
             cancellationToken);

         return Results.Ok(new
         {
             data = roles,
             message = "فهرست نقش‌ها."
         });
     })
     .RequireAuthorization("PlatformAdmin");

        endpoints.MapPost(
            "/api/admin/roles",
            async (
    CreateRoleRequest request,
    [FromServices] ISender sender,
    CancellationToken cancellationToken) =>
            {
                var code = request.Code?.Trim();
                var displayName = request.DisplayName?.Trim();

                if (string.IsNullOrWhiteSpace(code)
                    || code.Length > 100
                    || string.IsNullOrWhiteSpace(displayName)
                    || displayName.Length > 200)
                {
                    return Results.BadRequest(new
                    {
                        message =
                            "کد نقش و نام نمایشی الزامی هستند؛ حداکثر طول آن‌ها به‌ترتیب ۱۰۰ و ۲۰۰ کاراکتر است."
                    });
                }

                var result = await sender.Send(
     new CreateRoleCommand(code, displayName),
     cancellationToken);

                if (result.AlreadyExists)
                {
                    return Results.Conflict(new
                    {
                        message = "این کد نقش قبلاً ثبت شده است."
                    });
                }

                return Results.Created(
       $"/api/admin/roles/{result.RoleId}",
       new
       {
           data = new
           {
               roleId = result.RoleId,
               roleCode = result.RoleCode,
               displayName = result.DisplayName
           },
           message = "نقش ایجاد شد."
       });
            })
            .RequireAuthorization("PlatformAdmin");

        endpoints.MapPut(
            "/api/admin/roles/{roleId:guid}",
            async (
    Guid roleId,
    UpdateRoleRequest request,
    [FromServices] ISender sender,
    CancellationToken cancellationToken) =>
            {
                var displayName = request.DisplayName?.Trim();

                if (string.IsNullOrWhiteSpace(displayName)
                    || displayName.Length > 200)
                {
                    return Results.BadRequest(new
                    {
                        message =
                            "نام نمایشی نقش الزامی است و نباید بیشتر از ۲۰۰ کاراکتر باشد."
                    });
                }

                var result = await sender.Send(
     new RenameRoleCommand(roleId, displayName),
     cancellationToken);

                if (result is null)
                {
                    return Results.NotFound(new
                    {
                        message = "نقش پیدا نشد."
                    });
                }

                return Results.Ok(new
                {
                    data = new
                    {
                        roleId = result.RoleId,
                        roleCode = result.RoleCode,
                        displayName = result.DisplayName
                    },
                    message = "نقش به‌روزرسانی شد."
                });
            })
            .RequireAuthorization("PlatformAdmin");

        return endpoints;
    }
}

public sealed record CreateRoleRequest(
    string? Code,
    string? DisplayName);

public sealed record UpdateRoleRequest(
    string? DisplayName);