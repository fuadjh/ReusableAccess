using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using ReusableAccess.Infrastructure.Identity;
using System.Security.Claims;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace ReusableAccess.Api.Controllers;

public sealed class AuthorizationController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IOpenIddictScopeManager _scopeManager;
    public AuthorizationController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IOpenIddictScopeManager scopeManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _scopeManager = scopeManager;
    }

    [HttpGet("~/connect/authorize")]
    public async Task<IActionResult> Authorize()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException(
                "درخواست OpenID Connect پیدا نشد.");

        var authentication = await HttpContext.AuthenticateAsync(
            IdentityConstants.ApplicationScheme);

        var returnUrl =
            $"{Request.PathBase}{Request.Path}{Request.QueryString}";

        if (!authentication.Succeeded ||
            authentication.Principal is null)
        {
            return Challenge(
                new AuthenticationProperties { RedirectUri = returnUrl },
                IdentityConstants.ApplicationScheme);
        }

        var userId = authentication.Principal
            .FindFirstValue(ClaimTypes.NameIdentifier);

        var user = string.IsNullOrWhiteSpace(userId)
            ? null
            : await _userManager.FindByIdAsync(userId);

        if (user is null || !await _signInManager.CanSignInAsync(user))
        {
            await _signInManager.SignOutAsync();

            return Challenge(
                new AuthenticationProperties { RedirectUri = returnUrl },
                IdentityConstants.ApplicationScheme);
        }

        var identity = new ClaimsIdentity(
            authenticationType: TokenValidationParameters.DefaultAuthenticationType,
            nameType: Claims.Name,
            roleType: Claims.Role);

        identity.SetClaim(Claims.Subject, userId!);

        var userName = await _userManager.GetUserNameAsync(user);

        if (!string.IsNullOrWhiteSpace(userName))
        {
            identity.SetClaim(Claims.Name, userName);
        }

        var requestedScopes = request.GetScopes();
        identity.SetScopes(requestedScopes);
        var resources = new List<string>();

        await foreach (var resource in _scopeManager.ListResourcesAsync(requestedScopes))
        {
            resources.Add(resource);
        }

        identity.SetResources(resources);
        identity.SetDestinations(claim => claim.Type switch
        {
            Claims.Subject =>
            [
                Destinations.AccessToken,
                Destinations.IdentityToken
            ],

            Claims.Name when requestedScopes.Contains(Scopes.Profile) =>
            [
                Destinations.AccessToken,
                Destinations.IdentityToken
            ],

            _ => []
        });

        return SignIn(
            new ClaimsPrincipal(identity),
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}