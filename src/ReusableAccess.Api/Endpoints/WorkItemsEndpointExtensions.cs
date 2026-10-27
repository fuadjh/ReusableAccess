using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.WorkItems;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Domain.Enums;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Api.Endpoints;

public static class WorkItemsEndpointExtensions
{
    private const string ActiveScopeHeader = "X-Active-Scope";
    private const string ActiveScopeModeHeader = "X-Active-Scope-Mode";

    public static IEndpointRouteBuilder MapWorkItemsEndpoint(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
    "/api/work-items",
    async (
        HttpContext context,
        [FromServices] ISender sender,
        CancellationToken cancellationToken) =>
    {
        var modeValue =
            context.Request.Headers[ActiveScopeModeHeader].ToString();

        if (!TryParseScopeMode(modeValue, out var scopeMode))
        {
            return Results.BadRequest(
                "X-Active-Scope-Mode باید selected یا all باشد.");
        }

        string? scopeCode = null;

        if (scopeMode == ScopeSelectionMode.Selected)
        {
            scopeCode =
                context.Request.Headers[ActiveScopeHeader].ToString();

            if (string.IsNullOrWhiteSpace(scopeCode))
            {
                return Results.BadRequest("X-Active-Scope الزامی است.");
            }
        }

        var items = await sender.Send(
            new GetWorkItemsQuery(scopeMode, scopeCode),
            cancellationToken);

        if (items is null)
        {
            return Results.Forbid();
        }

        var message = scopeMode == ScopeSelectionMode.All
            ? "موارد کاری همهٔ محدوده‌های مجاز."
            : "موارد کاری محدودهٔ فعال.";

        return Results.Ok(new { data = items, message });
    })
    .RequireAuthorization("Permission:workitems.read");

        return endpoints;
    }

    private static bool TryParseScopeMode(
        string? value,
        out ScopeSelectionMode scopeMode)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            string.Equals(
                value.Trim(),
                "selected",
                StringComparison.OrdinalIgnoreCase))
        {
            scopeMode = ScopeSelectionMode.Selected;
            return true;
        }

        if (string.Equals(
            value.Trim(),
            "all",
            StringComparison.OrdinalIgnoreCase))
        {
            scopeMode = ScopeSelectionMode.All;
            return true;
        }

        scopeMode = default;
        return false;
    }
}