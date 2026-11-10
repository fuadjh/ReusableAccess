using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReusableAccess.Api.Endpoints;
using ReusableAccess.Application.Scopes;

namespace ReusableAccess.Api.Controllers;

[ApiController]
[Route("api/admin/scopes")]
[Authorize(Policy = "PlatformAdmin")]
public sealed class AdminScopesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        CancellationToken cancellationToken)
    {
        var scopes = await sender.Send(
            new GetScopesQuery(),
            cancellationToken);

        return Ok(new
        {
            data = scopes,
            message = "فهرست Scopeها."
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateScopeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(
                new CreateScopeCommand(
                    request.Code ?? string.Empty,
                    request.DisplayName ?? string.Empty,
                    request.ParentScopeId),
                cancellationToken);

            if (result.Status == ScopeCreationStatus.CodeConflict)
            {
                return Conflict(new
                {
                    message = "این کد Scope قبلاً ثبت شده است."
                });
            }

            if (result.Status == ScopeCreationStatus.ParentNotFound)
            {
                return BadRequest(new
                {
                    message = "Scope والد پیدا نشد."
                });
            }

            if (result.Status != ScopeCreationStatus.Created ||
                result.Data is null)
            {
                throw new InvalidOperationException(
                    "Unexpected scope creation result.");
            }

            return Created(
                $"/api/admin/scopes/{result.Data.ScopeId}",
                new
                {
                    data = result.Data,
                    message = "Scope ایجاد شد."
                });
        }
        catch (ScopeInputException error)
        {
            return BadRequest(new
            {
                message = error.Message
            });
        }
    }

    [HttpPut("{scopeId:guid}")]
    public async Task<IActionResult> Rename(
        Guid scopeId,
        [FromBody] UpdateScopeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var scope = await sender.Send(
                new RenameScopeCommand(
                    scopeId,
                    request.DisplayName ?? string.Empty),
                cancellationToken);

            if (scope is null)
            {
                return NotFound(new
                {
                    message = "Scope پیدا نشد."
                });
            }

            return Ok(new
            {
                data = scope,
                message = "Scope به‌روزرسانی شد."
            });
        }
        catch (ScopeInputException error)
        {
            return BadRequest(new
            {
                message = error.Message
            });
        }
    }

    [HttpDelete("{scopeId:guid}")]
    public async Task<IActionResult> Delete(
        Guid scopeId,
        CancellationToken cancellationToken)
    {
        var status = await sender.Send(
            new DeleteScopeCommand(scopeId),
            cancellationToken);

        if (status == ScopeDeletionStatus.Deleted)
            return Ok(new { data = true });

        if (status == ScopeDeletionStatus.NotFound)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "درخواست انجام نشد",
                detail: "محدوده پیدا نشد.");
        }

        if (status == ScopeDeletionStatus.HasDependencies)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "درخواست انجام نشد",
                detail: "محدوده دارای فرزند، تخصیص یا داده است.");
        }

        if (status == ScopeDeletionStatus.ConcurrencyConflict)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "درخواست انجام نشد",
                detail: "اطلاعات هم‌زمان تغییر کرده؛ دوباره دریافت کنید.");
        }

        throw new InvalidOperationException(
            "Unexpected scope deletion result.");
    }
}