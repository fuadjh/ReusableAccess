using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Components.Authorization;
using ReusableAccess.Web.Components;
using ReusableAccess.Web.Infrastructure;
using System.Net.Http.Headers;

using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;

var builder = WebApplication.CreateBuilder(args);
const string ActiveRoleTicketKey = "ReusableAccess.ActiveRole";
const string ActiveScopeTicketKey = "ReusableAccess.ActiveScope";
var clientSecret =
    builder.Configuration["Authentication:OpenIddict:ClientSecret"];

if (string.IsNullOrWhiteSpace(clientSecret))
{
    throw new InvalidOperationException(
        "OpenIddict client secret is not configured for the Web app.");
}
builder.Services.AddDistributedMemoryCache();
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

        // در این گام توکن‌ها را در کوکی Web ذخیره نمی‌کنیم.
        options.SaveTokens = true;

        options.Scope.Add("profile");
        options.Scope.Add("access_api");
        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = "name";
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var apiBaseAddress = builder.Configuration["Api:BaseAddress"]
    ?? throw new InvalidOperationException(
        "Api:BaseAddress is not configured.");

builder.Services.AddHttpClient("Api", client =>
{
    client.BaseAddress = new Uri(apiBaseAddress);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
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
// توکن ضد-CSRF را برای درخواست تغییر پروفایل به مرورگر می‌دهد.
// این endpoint توکن دسترسی API را برنمی‌گرداند.
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


// انتخاب پروفایل مجاز و ذخیرهٔ آن در ticket سمت‌سرور.
app.MapPost(
    "/bff/access-profile/select",
    async (
        HttpContext context,
        IHttpClientFactory httpClientFactory,
        IAntiforgery antiforgery,
        CancellationToken cancellationToken) =>
    {
        app.Logger.LogWarning("BFF_PROFILE_SELECT_HANDLER_REACHED");
        // چون این درخواست با cookie احراز هویت انجام می‌شود،
        // درخواست باید توکن ضد-CSRF معتبر داشته باشد.
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
            // از API می‌پرسیم این کاربر واقعاً چه پروفایل‌هایی دارد.
            // نقش و محدودهٔ ارسالی از Web به‌تنهایی مجوز ایجاد نمی‌کند.
            var apiClient = httpClientFactory.CreateClient("Api");

            using var profilesRequest = new HttpRequestMessage(
                HttpMethod.Get,
                "api/me/access-profiles");

            profilesRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            using var profilesResponse = await apiClient.SendAsync(
                profilesRequest,
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
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            // مقادیر در AuthenticationProperties ذخیره می‌شوند.
            // چون Cookie به ITicketStore وصل است، ticket سمت‌سرور به‌روزرسانی می‌شود.
            authentication.Properties.Items[ActiveRoleTicketKey] =
                selection.RoleCode;

            authentication.Properties.Items[ActiveScopeTicketKey] =
                selection.ScopeCode;

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


// endpoint تشخیصی اکنون پروفایل را از ticket سمت‌سرور می‌خواند،
// نه از query string مرورگر.
app.MapGet(
    "/bff/diagnostics/protected",
    async (
        HttpContext context,
        IHttpClientFactory httpClientFactory,
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

        authentication.Properties.Items.TryGetValue(
      ActiveRoleTicketKey,
      out var roleCode);

        authentication.Properties.Items.TryGetValue(
            ActiveScopeTicketKey,
            out var scopeCode);

        if (string.IsNullOrWhiteSpace(roleCode) ||
            string.IsNullOrWhiteSpace(scopeCode))
        {
            return Results.Conflict(
                "ابتدا از صفحهٔ /access-profiles یک پروفایل فعال انتخاب کن.");
        }

        var client = httpClientFactory.CreateClient("Api");

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "api/diagnostics/protected");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        request.Headers.TryAddWithoutValidation(
            "X-Active-Role",
            roleCode);

        request.Headers.TryAddWithoutValidation(
            "X-Active-Scope",
            scopeCode);

        try
        {
            using var response = await client.SendAsync(
                request,
                cancellationToken);

            var body = await response.Content.ReadAsStringAsync(
                cancellationToken);

            var contentType =
                response.Content.Headers.ContentType?.ToString()
                ?? "application/json";

            return Results.Content(
                body,
                contentType,
                statusCode: (int)response.StatusCode);
        }
        catch (HttpRequestException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "ارتباط با API برقرار نشد.");
        }
    })
    .RequireAuthorization();
// توکن فقط در سمت سرور خوانده و به API فرستاده می‌شود؛
// پاسخ BFF شامل خود توکن نیست.
app.MapGet(
    "/bff/access-profiles",
    async (
        HttpContext context,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken) =>
    {
        var accessToken = await context.GetTokenAsync("access_token");

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Access token is not available in the server-side ticket.");
        }

        var client = httpClientFactory.CreateClient("Api");

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "api/me/access-profiles");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await client.SendAsync(
                request,
                cancellationToken);

            var body = await response.Content.ReadAsStringAsync(
                cancellationToken);

            var contentType =
                response.Content.Headers.ContentType?.ToString()
                ?? "application/json";

            return Results.Content(
                body,
                contentType,
                statusCode: (int)response.StatusCode);
        }
        catch (HttpRequestException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "The API could not be reached.");
        }
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