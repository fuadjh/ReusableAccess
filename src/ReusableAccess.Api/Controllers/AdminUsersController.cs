using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReusableAccess.Api.Completion;
using ReusableAccess.Application.Completion;
using ReusableAccess.Contracts;

namespace ReusableAccess.Api.Controllers;

// این گام فقط مرز HTTP را جابه‌جا می‌کند؛ قواعد کسب‌وکار در Application می‌مانند.
[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = "PlatformAdmin")]
public sealed class AdminUsersController(ISender sender) : ControllerBase
{
    [HttpGet("manage")]
    public async Task<IActionResult> List(
        [FromQuery] string? q,
        [FromQuery] int? page,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new SearchUsers(q, page ?? 1), cancellationToken);

        return Ok(new { data = result });
    }

    [HttpGet("{id:guid}/details")]
    public async Task<IActionResult> Details(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetCurrentUser(id), cancellationToken);

        return Ok(new { data = result });
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateUserInput input,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateManagedUser(ActorId, input), cancellationToken);

        // برای سازگاری با مصرف‌کننده‌های موجود، قرارداد فعلی 200 + data حفظ می‌شود.
        return Ok(new { data = result });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateUserInput input,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateManagedUser(ActorId, id, input), cancellationToken);

        return Ok(new { data = result });
    }

    [HttpPut("{id:guid}/active")]
    public async Task<IActionResult> SetActive(
        Guid id,
        [FromBody] ActiveUserInput input,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new SetManagedUserActive(ActorId, id, input), cancellationToken);

        return Ok(new { data = result });
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(
        Guid id,
        [FromBody] AdminPasswordInput input,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ResetManagedPassword(ActorId, id, input.Password),
            cancellationToken);

        return Ok(new { data = result });
    }

    // شناسهٔ عامل از هویت معتبر سرور می‌آید، نه از JSON یا پارامتر ورودی کاربر.
    private Guid ActorId => CompletionApiExtensions.Subject(HttpContext);
}