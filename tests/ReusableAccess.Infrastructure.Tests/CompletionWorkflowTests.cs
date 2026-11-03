using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReusableAccess.Application.Completion;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Domain.Identity;
using ReusableAccess.Infrastructure;
using ReusableAccess.Infrastructure.Identity;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure.Tests;

public sealed class CompletionWorkflowTests
{
    [Fact]
    public async Task Closed_registration_cannot_be_bypassed()
    {
        await using var f = await Fixture.Create();

        var e = await Assert.ThrowsAsync<CompletionException>(() =>
            f.Store.CreateUserAsync(f.Input(1), false, default));

        Assert.Equal(403, e.StatusCode);
    }

    [Fact]
    public async Task Admin_cannot_disable_self_or_last_admin()
    {
        await using var f = await Fixture.Create();
        var admin = await f.User(1, true);

        var self = await Assert.ThrowsAsync<CompletionException>(() =>
            f.Store.SetActiveAsync(
                admin.Id, admin.Id,
                new(false, admin.ConcurrencyStamp!), default));

        Assert.Equal(409, self.StatusCode);

        var last = await Assert.ThrowsAsync<CompletionException>(() =>
            f.Store.SetActiveAsync(
                Guid.NewGuid(), admin.Id,
                new(false, admin.ConcurrencyStamp!), default));

        Assert.Equal(409, last.StatusCode);
    }

    [Fact]
    public async Task Deactivation_invalidates_session()
    {
        await using var f = await Fixture.Create();
        var user = await f.User(1);

        Assert.True(await f.Validator.IsCurrentAsync(user.Id, 0, default));

        await f.Store.SetActiveAsync(
            Guid.NewGuid(), user.Id,
            new(false, user.ConcurrencyStamp!), default);

        Assert.False(await f.Validator.IsCurrentAsync(user.Id, 0, default));
    }

    [Fact]
    public async Task Cross_scope_work_item_cannot_be_updated()
    {
        await using var f = await Fixture.Create();

        var a = new AccessScope("A", "A");
        var b = new AccessScope("B", "B");
        var item = new WorkItem(b.Id, "B item", "Open");

        f.Db.AddRange(a, b, item);
        await f.Db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<CompletionException>(() =>
            f.Store.WriteWorkItemAsync(
                item.Id, "selected", "A",
                new(Guid.Empty, "changed", "Closed"), default));

        Assert.Equal(404, error.StatusCode);
        Assert.Equal("B item", item.Title);
    }

    [Fact]
    public async Task Mfa_requires_verified_admin_channel()
    {
        await using var f = await Fixture.Create();
        var admin = await f.User(1, true);
        var view = await f.Store.SettingsAsync(default);

        view.Policy.LoginMode = LoginMode.PasswordAndEmail;

        var error = await Assert.ThrowsAsync<CompletionException>(() =>
            f.Store.SaveSettingsAsync(admin.Id, view.Policy, default));

        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public async Task Contact_code_is_single_use()
    {
        await using var f = await Fixture.Create();
        var user = await f.User(1);

        var id = await f.Workflow.RequestContactAsync(
            user.Id, DeliveryChannel.Email, default);

        var code = f.Sender.Code!;

        await f.Workflow.ConfirmContactAsync(user.Id, id, code, default);

        Assert.True(
            (await f.Store.CurrentUserAsync(user.Id, default)).EmailConfirmed);

        await Assert.ThrowsAsync<CompletionException>(() =>
            f.Workflow.ConfirmContactAsync(user.Id, id, code, default));
    }

    [Fact]
    public async Task Contact_code_expires()
    {
        await using var f = await Fixture.Create();
        var user = await f.User(1);

        var id = await f.Workflow.RequestContactAsync(
            user.Id, DeliveryChannel.Email, default);

        f.Clock.Now = f.Clock.Now.AddMinutes(5);

        await Assert.ThrowsAsync<CompletionException>(() =>
            f.Workflow.ConfirmContactAsync(
                user.Id, id, f.Sender.Code!, default));
    }

    [Fact]
    public async Task Five_wrong_attempts_block_the_code()
    {
        await using var f = await Fixture.Create();
        var user = await f.User(1);

        var id = await f.Workflow.RequestContactAsync(
            user.Id, DeliveryChannel.Email, default);

        var correct = f.Sender.Code!;

        for (var i = 0; i < 5; i++)
        {
            await Assert.ThrowsAsync<CompletionException>(() =>
                f.Workflow.ConfirmContactAsync(
                    user.Id, id, "000000", default));
        }

        await Assert.ThrowsAsync<CompletionException>(() =>
            f.Workflow.ConfirmContactAsync(user.Id, id, correct, default));
    }

    [Fact]
    public async Task Recovery_off_rejects_direct_request()
    {
        await using var f = await Fixture.Create();

        var e = await Assert.ThrowsAsync<CompletionException>(() =>
            f.Workflow.RequestRecoveryAsync(f.Input(1).NationalId, default));

        Assert.Equal(403, e.StatusCode);
    }

    [Fact]
    public async Task Mfa_requires_password_then_code()
    {
        await using var f = await Fixture.Create();
        var user = await f.User(1);

        var policy = await f.Db.Set<AuthenticationPolicy>().SingleAsync();
        policy.LoginMode = LoginMode.PasswordAndEmail;
        await f.Db.SaveChangesAsync();

        var start = await f.Workflow.PasswordAsync(
            user.NationalId, Fixture.Password, false, default);

        Assert.False(start.Succeeded);
        Assert.NotNull(start.ChallengeId);

        var end = await f.Workflow.VerifyLoginAsync(
            start.ChallengeId!.Value, f.Sender.Code!, default);

        Assert.True(end.Succeeded);
        // فقط نتیجهٔ متد را بررسی نمی‌کنیم؛ principal و صدور کوکی هم بررسی می‌شوند.
        Assert.True(f.Http.User.Identity?.IsAuthenticated == true);

        Assert.False(
            string.IsNullOrWhiteSpace(
                f.Http.Response.Headers["Set-Cookie"].ToString()));
    }

    [Fact]
    public async Task Temporary_password_must_be_changed_before_session()
    {
        await using var f = await Fixture.Create();
        var user = await f.User(1);

        user.MustChangePassword = true;
        await f.Users.UpdateAsync(user);

        var start = await f.Workflow.PasswordAsync(
            user.NationalId, Fixture.Password, false, default);

        Assert.False(start.Succeeded);
        Assert.True(start.PasswordChangeRequired);

        var end = await f.Workflow.ChangeRequiredPasswordAsync(
            start.ChallengeId!.Value, "New-Only.Strong72!", default);

        Assert.True(end.Succeeded);
        Assert.False(await f.Users.CheckPasswordAsync(user, Fixture.Password));
    }

    private sealed class FakeSender : IAccountMessageSender
    {
        public string? Code;

        public bool IsConfigured(DeliveryChannel channel) => true;

        public Task SendAsync(
            DeliveryChannel channel, string destination,
            string code, CancellationToken ct)
        {
            Code = code;
            return Task.CompletedTask;
        }
    }
    private sealed class TestHttpContextAccessor : IHttpContextAccessor
    {
        // فقط برای تستِ فاقد درخواست HTTP واقعی:
        // یک reference معمولی نگه می‌داریم، نه مقدار وابسته به AsyncLocal.
        public HttpContext? HttpContext { get; set; }
    }
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now =
            new(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public const string Password = "Test-Only.Strong42!";

        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _root;
        private readonly AsyncServiceScope _scope;

        public Clock Clock { get; }
        public FakeSender Sender { get; }
        public HttpContext Http =>
    _scope.ServiceProvider
        .GetRequiredService<IHttpContextAccessor>()
        .HttpContext
    ?? throw new InvalidOperationException(
        "The test HttpContext has not been initialized.");
        public ApplicationDbContext Db => _scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        public UserManager<ApplicationUser> Users => _scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        public ICompletionStore Store => _scope.ServiceProvider
            .GetRequiredService<ICompletionStore>();

        public IAccountWorkflow Workflow => _scope.ServiceProvider
            .GetRequiredService<IAccountWorkflow>();

        public IAccountSessionValidator Validator => _scope.ServiceProvider
            .GetRequiredService<IAccountSessionValidator>();

        private Fixture(
            SqliteConnection connection, ServiceProvider root,
            Clock clock, FakeSender sender)
        {
            _connection = connection;
            _root = root;
            _scope = root.CreateAsyncScope();
            Clock = clock;
            Sender = sender;
        }

        public static async Task<Fixture> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var services = new ServiceCollection();

            var clock = new Clock();
            var sender = new FakeSender();

            // هر Fixture و ServiceProvider تست، نمونهٔ مستقل خودش را دارد.
            var contextAccessor = new TestHttpContextAccessor();

            services.AddSingleton<TimeProvider>(clock);
            services.AddSingleton<IHttpContextAccessor>(contextAccessor);

            services.AddLogging();
            services.AddDataProtection();

            services.AddAuthentication()
                .AddCookie(IdentityConstants.ApplicationScheme);

            services.AddDbContext<ApplicationDbContext>(
                options => options.UseSqlite(connection));

            services.AddIdentityCore<ApplicationUser>()
                .AddSignInManager()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            services.AddCompletionInfrastructure();

            // Sender جعلی فقط در همین ServiceProvider تست جایگزین می‌شود.
            services.AddSingleton<IAccountMessageSender>(sender);

            var fixture = new Fixture(
                connection,
                services.BuildServiceProvider(),
                clock,
                sender);

            await fixture.Db.Database.EnsureCreatedAsync();

            var httpContext = new DefaultHttpContext
            {
                RequestServices = fixture._scope.ServiceProvider
            };

            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new HostString("localhost");

            // این بار مقدار در property معمولیِ accessor تست باقی می‌ماند.
            contextAccessor.HttpContext = httpContext;

            return fixture;
        }
        public CreateUserInput Input(int number)
        {
            var prefix = "00110000" + number;

            var remainder = Enumerable.Range(0, 9)
                .Sum(i => (prefix[i] - '0') * (10 - i)) % 11;

            var national = prefix +
                (remainder < 2 ? remainder : 11 - remainder);

            return new()
            {
                FirstName = "Test",
                LastName = "User",
                OrganizationalCode = "T" + number,
                NationalId = national,
                Email = $"t{number}@example.invalid",
                Mobile = $"+99910000000{number}",
                Password = Password
            };
        }

        public async Task<ApplicationUser> User(int number, bool admin = false)
        {
            var p = Input(number);

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = p.NationalId,
                NationalId = p.NationalId,
                FirstName = p.FirstName,
                LastName = p.LastName,
                DisplayName = "Test User",
                OrganizationalCode = p.OrganizationalCode,
                Email = p.Email,
                PhoneNumber = p.Mobile,
                IsActive = true,
                IsPlatformSuperAdmin = admin,
                LockoutEnabled = true
            };

            var result = await Users.CreateAsync(user, Password);

            Assert.True(
                result.Succeeded,
                string.Join(";", result.Errors.Select(x => x.Description)));

            return user;
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _root.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}