using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Validation.AspNetCore;
using ReusableAccess.Api.Contracts;

namespace ReusableAccess.Api.Controllers;

[ApiController]
[Route("api/diagnostics")]
public sealed class AuthProbeController : ControllerBase
{
    [HttpGet("protected")]
    [Authorize(
        AuthenticationSchemes =
            OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
    public ActionResult<ApiResponse<string>> Get()
    {
        return Ok(new ApiResponse<string>(
            "authenticated",
            "توکن معتبر است."));
    }
}