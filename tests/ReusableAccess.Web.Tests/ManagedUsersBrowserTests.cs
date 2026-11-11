using Microsoft.Playwright;
using System.Security.Cryptography;
using Xunit;
using static Microsoft.Playwright.Assertions;
using static System.Net.Mime.MediaTypeNames;
using Assert = Xunit.Assert;

namespace ReusableAccess.Web.Tests;

public sealed class UserBrowserFactAttribute : FactAttribute
{
    public UserBrowserFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("RA_E2E_BASE_URL")) ||
            string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("RA_E2E_STORAGE_STATE")))
        {
            Skip = "E2E URL/authentication state is not configured.";
        }
    }
}

[Trait("Category", "Browser")]
public sealed class ManagedUsersBrowserTests
{
    [UserBrowserFact]
    public async Task User_modals_support_create_cancel_edit_reset_and_deactivate()
    {
        Assert.Equal(
            "YES_TEST_ONLY",
            Environment.GetEnvironmentVariable("RA_E2E_ALLOW_WRITES"));

        var baseUrl = Environment.GetEnvironmentVariable("RA_E2E_BASE_URL")!;
        var statePath = Environment.GetEnvironmentVariable("RA_E2E_STORAGE_STATE")!;

        var address = new Uri(baseUrl);
        Assert.True(address.IsLoopback, "Use a local isolated test deployment.");
        Assert.True(File.Exists(statePath), "Authentication state file is missing.");

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        await using var context = await browser.NewContextAsync(new()
        {
            StorageStatePath = statePath
        });

        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(20_000);

        await page.GotoAsync(
            new Uri(address, "/console/users").AbsoluteUri);

        var nationalId = NewNationalId();
        var email = $"e2e-{Guid.NewGuid():N}@example.test";

        await page.GetByTestId("user-create")
            .GetByRole(AriaRole.Button).ClickAsync();

        await Field(page, "user-first-name").FillAsync("E2E");
        await Field(page, "user-last-name").FillAsync("Probe");
        await Field(page, "user-organizational-code").FillAsync("E2E-TEST");
        await Field(page, "user-national-id").FillAsync(nationalId);
        await Field(page, "user-email").FillAsync(email);
        await Field(page, "user-mobile").FillAsync("09123456789");
        await Field(page, "user-initial-password")
            .FillAsync("Test-Only.Strong42!");

        await page.GetByRole(
            AriaRole.Button,
            new() { Name = "ذخیره کاربر", Exact = true }).ClickAsync();

        await Expect(page.GetByTestId("user-editor")).ToBeHiddenAsync();

        await Field(page, "user-search").FillAsync(nationalId);
        await page.GetByRole(
            AriaRole.Button,
            new() { Name = "جست‌وجو", Exact = true }).ClickAsync();

        var row = page.Locator("tr").Filter(new() { HasText = nationalId });
        await Expect(row).ToHaveCountAsync(1);

        // انصراف از فرم نباید داده را تغییر دهد.
        await row.GetByRole(
            AriaRole.Button,
            new() { Name = "ویرایش", Exact = true }).ClickAsync();

        await Field(page, "user-first-name").FillAsync("MustNotSave");

        await page.GetByRole(
            AriaRole.Button,
            new() { Name = "انصراف", Exact = true }).ClickAsync();

        await Expect(page.GetByTestId("user-editor")).ToBeHiddenAsync();

        await row.GetByRole(
            AriaRole.Button,
            new() { Name = "ویرایش", Exact = true }).ClickAsync();

        await Expect(Field(page, "user-first-name")).ToHaveValueAsync("E2E");
        await Field(page, "user-first-name").FillAsync("EditedE2E");

        await page.GetByRole(
            AriaRole.Button,
            new() { Name = "ذخیره کاربر", Exact = true }).ClickAsync();

        await Expect(page.GetByTestId("user-editor")).ToBeHiddenAsync();
        await Expect(row).ToContainTextAsync("EditedE2E");

        await row.GetByRole(
            AriaRole.Button,
            new() { Name = "بازنشانی رمز", Exact = true }).ClickAsync();

        await Field(page, "user-reset-password")
            .FillAsync("Reset-Only.Strong84!");
        await Field(page, "user-reset-confirmation")
            .FillAsync("Reset-Only.Strong84!");

        await page.GetByRole(
            AriaRole.Button,
            new() { Name = "بازنشانی و ابطال نشست‌ها", Exact = true })
            .ClickAsync();

        await Expect(page.GetByTestId("user-password")).ToBeHiddenAsync();

        await row.GetByRole(
            AriaRole.Button,
            new() { Name = "غیرفعال‌سازی", Exact = true }).ClickAsync();

        await page.GetByRole(
            AriaRole.Button,
            new() { Name = "تأیید تغییر وضعیت", Exact = true }).ClickAsync();

        await Expect(row.GetByText(
            "غیرفعال", new() { Exact = true })).ToBeVisibleAsync();
    }

    private static ILocator Field(IPage page, string testId)
        => page.GetByTestId(testId).Locator("input");

    private static string NewNationalId()
    {
        while (true)
        {
            var prefix = RandomNumberGenerator
                .GetInt32(100_000_000, 999_999_999)
                .ToString(System.Globalization.CultureInfo.InvariantCulture);

            var sum = Enumerable.Range(0, 9)
                .Sum(index => (prefix[index] - '0') * (10 - index));

            var remainder = sum % 11;
            var check = remainder < 2 ? remainder : 11 - remainder;
            var result = prefix + check;

            if (result.Distinct().Count() > 1)
                return result;
        }
    }
}