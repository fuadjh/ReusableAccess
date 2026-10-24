using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Scopes;
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
         [FromServices] ISender sender,
         CancellationToken cancellationToken) =>
     {
         var scopes = await sender.Send(
             new GetScopesQuery(),
             cancellationToken);

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
    [FromServices] ISender sender,
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

                var result = await sender.Send(
     new CreateScopeCommand(
         code,
         displayName,
         request.ParentScopeId),
     cancellationToken);

                if (result.Status == ScopeCreationStatus.CodeConflict)
                {
                    return Results.Conflict(new
                    {
                        message = "این کد Scope قبلاً ثبت شده است."
                    });
                }

                if (result.Status == ScopeCreationStatus.ParentNotFound)
                {
                    return Results.BadRequest(new
                    {
                        message = "Scope والد پیدا نشد."
                    });
                }

                var scope = result.Data!;

                return Results.Created(
                    $"/api/admin/scopes/{scope.ScopeId}",
                    new
                    {
                        data = new
                        {
                            scopeId = scope.ScopeId,
                            scopeCode = scope.ScopeCode,
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
                      "نام نمایشی Scope الزامی است و حداکثر ۲۰۰ کاراکتر دارد."
              });
          }

          var result = await sender.Send(
              new RenameScopeCommand(scopeId, displayName),
              cancellationToken);

          if (result is null)
          {
              return Results.NotFound(new
              {
                  message = "Scope پیدا نشد."
              });
          }

          return Results.Ok(new
          {
              data = new
              {
                  scopeId = result.ScopeId,
                  scopeCode = result.ScopeCode,
                  displayName = result.DisplayName,
                  parentScopeId = result.ParentScopeId
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