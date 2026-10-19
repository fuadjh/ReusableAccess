using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Components.Authorization;
using ReusableAccess.Web.Components;
using ReusableAccess.Web.Infrastructure;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

var clientSecret =
    builder.Configuration["Authentication:OpenIddict:ClientSecret"];

if (string.IsNullOrWhiteSpace(clientSecret))
{
    throw new InvalidOperationException(
        "OpenIddict client secret is not configured for the Web app.");
}

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSingleton<ActiveAccessProfileStore>();
builder.Services.AddScoped<BffApiClient>();
builder.Services.AddSingleton<ITicketStore, DistributedCacheTicketStore>();

builder.Services
    .AddOptions<CookieAuthenticationOptions>(
        CookieAuthenticationDefaults.AuthenticationScheme)
    .Configure<ITicketStore>((options, ticketStore) =>
    {
        options.SessionStore = ticketStore;
    });

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
});

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            CookieAuthenticationDefaults.AuthenticationScheme;

        options.DefaultSignInScheme =
            CookieAuthenticationDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = ".ReusableAccess.Web.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
    })
    .AddOpenIdConnect(options =>
    {
        options.Authority = "https://localhost:7042/";
        options.ClientId = "ReusableAccess.Web";
        options.ClientSecret = clientSecret;

        options.ResponseType = "code";
        options.UsePkce = true;

        // توکن‌ها در ticket سمت‌سرور ذخیره می‌شوند؛
        // مرورگر فقط کوکی نشست را دریافت می‌کند.
        options.SaveTokens = true;

        options.Scope.Add("profile");
        options.Scope.Add("access_api");
        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = "name";
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

var apiBaseAddress = builder.Configuration["Api:BaseAddress"]
    ?? throw new InvalidOperationException(
        "Api:BaseAddress is not configured.");

builder.Services.AddHttpClient("Api", client =>
{
    client.BaseAddress = new Uri(apiBaseAddress);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/bff"),
    branch =>
    {
        branch.UseStatusCodePagesWithReExecute(
            "/not-found",
            createScopeForStatusCodePages: true);
    });

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet(
    "/bff/antiforgery-token",
    (HttpContext context, IAntiforgery antiforgery) =>
    {
        var tokens = antiforgery.GetAndStoreTokens(context);

        return Results.Ok(new
        {
            requestToken = tokens.RequestToken
        });
    })
    .RequireAuthorization();

app.MapPost(
    "/bff/access-profile/select",
    async (
        HttpContext context,
        IAntiforgery antiforgery,
        ActiveAccessProfileStore activeProfileStore,
        BffApiClient bffApiClient,
        CancellationToken cancellationToken) =>
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.BadRequest(
                "درخواست ضد-CSRF معتبر نیست؛ صفحه را تازه‌سازی و دوباره امتحان کن.");
        }

        AccessProfileSelectionRequest? selection;

        try
        {
            selection =
                await context.Request.ReadFromJsonAsync<AccessProfileSelectionRequest>(
                    cancellationToken);
        }
        catch (JsonException)
        {
            return Results.BadRequest("بدنهٔ درخواست معتبر نیست.");
        }

        if (selection is null ||
            string.IsNullOrWhiteSpace(selection.RoleCode) ||
            string.IsNullOrWhiteSpace(selection.ScopeCode))
        {
            return Results.BadRequest("roleCode و scopeCode الزامی هستند.");
        }

        var authentication = await context.AuthenticateAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);

        if (!authentication.Succeeded ||
            authentication.Principal is null ||
            authentication.Properties is null)
        {
            return Results.Unauthorized();
        }

        var accessToken = await context.GetTokenAsync("access_token");

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "توکن دسترسی در ticket سمت‌سرور موجود نیست.");
        }

        try
        {
            using var profilesResponse = await bffApiClient.GetAsync(
                "api/me/access-profiles",
                accessToken,
                activeProfile: null,
                cancellationToken);

            if (!profilesResponse.IsSuccessStatusCode)
            {
                return Results.StatusCode(
                    (int)profilesResponse.StatusCode);
            }

            await using var responseStream =
                await profilesResponse.Content.ReadAsStreamAsync(
                    cancellationToken);

            using var json = await JsonDocument.ParseAsync(
                responseStream,
                cancellationToken: cancellationToken);

            var data = json.RootElement.GetProperty("data");

            var profileIsAssigned = data
                .EnumerateArray()
                .Any(profile =>
                    string.Equals(
                        profile.GetProperty("roleCode").GetString(),
                        selection.RoleCode,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        profile.GetProperty("scopeCode").GetString(),
                        selection.ScopeCode,
                        StringComparison.Ordinal));

            if (!profileIsAssigned)
            {
                return Results.StatusCode(
                    StatusCodes.Status403Forbidden);
            }

            activeProfileStore.Set(
                authentication.Properties,
                selection.RoleCode,
                selection.ScopeCode);

            await context.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                authentication.Principal,
                authentication.Properties);

            return Results.Ok(new
            {
                roleCode = selection.RoleCode,
                scopeCode = selection.ScopeCode,
                message = "پروفایل فعال ذخیره شد."
            });
        }
        catch (HttpRequestException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "ارتباط با API برقرار نشد.");
        }
        catch (JsonException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "پاسخ فهرست پروفایل‌ها از API قابل خواندن نبود.");
        }
    })
    .RequireAuthorization();

app.MapGet(
    "/bff/diagnostics/protected",
    async (
        HttpContext context,
        ActiveAccessProfileStore activeProfileStore,
        BffApiClient bffApiClient,
        CancellationToken cancellationToken) =>
    {
        var authentication = await context.AuthenticateAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);

        if (!authentication.Succeeded ||
            authentication.Properties is null)
        {
            return Results.Unauthorized();
        }

        var accessToken = await context.GetTokenAsync("access_token");

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "توکن دسترسی در ticket سمت‌سرور موجود نیست.");
        }

        if (!activeProfileStore.TryGet(
                authentication.Properties,
                out var activeProfile))
        {
            return Results.Conflict(
                "ابتدا از صفحهٔ /access-profiles یک پروفایل فعال انتخاب کن.");
        }

        return await bffApiClient.ForwardGetAsync(
            "api/diagnostics/protected",
            accessToken,
            activeProfile,
            cancellationToken);
    })
    .RequireAuthorization();

app.MapGet(
    "/bff/work-items",
    async (
        HttpContext context,
        ActiveAccessProfileStore activeProfileStore,
        BffApiClient bffApiClient,
        CancellationToken cancellationToken) =>
    {
        var authentication = await context.AuthenticateAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);

        if (!authentication.Succeeded ||
            authentication.Properties is null)
        {
            return Results.Unauthorized();
        }

        var accessToken = await context.GetTokenAsync("access_token");

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "توکن دسترسی در ticket سمت‌سرور موجود نیست.");
        }

        if (!activeProfileStore.TryGet(
                authentication.Properties,
                out var activeProfile))
        {
            return Results.Conflict(
                "ابتدا یک پروفایل فعال انتخاب کن.");
        }

        return await bffApiClient.ForwardGetAsync(
            "api/work-items",
            accessToken,
            activeProfile,
            cancellationToken);
    })
    .RequireAuthorization();

app.MapGet(
    "/bff/access-profiles",
    async (
        HttpContext context,
        BffApiClient bffApiClient,
        CancellationToken cancellationToken) =>
    {
        var accessToken = await context.GetTokenAsync("access_token");

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "توکن دسترسی در ticket سمت‌سرور موجود نیست.");
        }

        return await bffApiClient.ForwardGetAsync(
            "api/me/access-profiles",
            accessToken,
            activeProfile: null,
            cancellationToken);
    })
    .RequireAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();

app.MapGet(
    "/account/login",
    (string? returnUrl) =>
    {
        var safeReturnUrl =
            !string.IsNullOrWhiteSpace(returnUrl) &&
            returnUrl.StartsWith("/", StringComparison.Ordinal) &&
            !returnUrl.StartsWith("//", StringComparison.Ordinal) &&
            !returnUrl.Contains('\\')
                ? returnUrl
                : "/";

        var properties = new AuthenticationProperties
        {
            RedirectUri = safeReturnUrl
        };

        return Results.Challenge(
            properties,
            new[] { OpenIdConnectDefaults.AuthenticationScheme });
    })
    .AllowAnonymous();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

internal sealed record AccessProfileSelectionRequest(
    string RoleCode,
    string ScopeCode);