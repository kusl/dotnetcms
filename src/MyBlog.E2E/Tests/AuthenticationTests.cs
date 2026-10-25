using Microsoft.Playwright;
using Xunit;

namespace MyBlog.E2E.Tests;

[Collection(PlaywrightCollection.Name)]
public sealed class AuthenticationTests(PlaywrightFixture fixture)
{
    private readonly PlaywrightFixture _fixture = fixture;

    [Fact]
    public async Task ProtectedRoute_RedirectsToLogin_WhenNotAuthenticated()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/admin");

        await page.WaitForURLAsync("**/login**", new() { Timeout = 10000 });

        var heading = page.Locator("h1");
        await Assertions.Expect(heading).ToContainTextAsync("Login");
    }

    [Fact]
    public async Task ProtectedRoute_IncludesReturnUrl_WhenRedirected()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/admin/posts");

        await page.WaitForURLAsync("**/login**", new() { Timeout = 10000 });

        Assert.Contains("returnUrl", page.Url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_WithDefaultCredentials_Succeeds()
    {
        var page = await _fixture.CreatePageAsync();

        await page.LoginAsAdminAsync();

        var adminHeading = page.Locator("h1");
        await Assertions.Expect(adminHeading).ToContainTextAsync("Admin", new() { Timeout = 10000 });
    }

    [Fact]
    public async Task Login_ShowsAdminLink_WhenAuthenticated()
    {
        var page = await _fixture.CreatePageAsync();

        await page.LoginAsAdminAsync();

        await page.GotoAsync("/");

        var adminLink = page.Locator("nav a[href='/admin']");
        await Assertions.Expect(adminLink).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task Logout_RedirectsToHome()
    {
        var page = await _fixture.CreatePageAsync();

        await page.LoginAsAdminAsync();

        var logoutButton = page.Locator("form[action='/logout'] button");
        await Assertions.Expect(logoutButton.First).ToBeVisibleAsync(new() { Timeout = 10000 });

        await Task.WhenAll(
            page.WaitForURLAsync("**/", new() { Timeout = 10000 }),
            logoutButton.First.ClickAsync()
        );

        var loginLink = page.Locator("nav a[href='/login']");
        await Assertions.Expect(loginLink).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task Logout_RemovesAdminLink()
    {
        var page = await _fixture.CreatePageAsync();

        await page.LoginAsAdminAsync();

        var logoutButton = page.Locator("form[action='/logout'] button");

        await Task.WhenAll(
            page.WaitForURLAsync("**/", new() { Timeout = 10000 }),
            logoutButton.First.ClickAsync()
        );

        var adminLink = page.Locator("nav a[href='/admin']");
        await Assertions.Expect(adminLink).Not.ToBeVisibleAsync(new() { Timeout = 5000 });
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ShowsError()
    {
        var page = await _fixture.CreatePageAsync();

        await page.SubmitLoginAsync(BlazorPageExtensions.AdminUsername, "WrongPassword!", "**error=invalid**");

        await Assertions.Expect(page.Locator(".error-message")).ToHaveTextAsync("Invalid username or password");
    }

    [Fact]
    public async Task Login_WithInvalidUsername_ShowsError()
    {
        var page = await _fixture.CreatePageAsync();

        await page.SubmitLoginAsync("nonexistentuser", "SomePassword123!", "**error=invalid**");

        await Assertions.Expect(page.Locator(".error-message")).ToHaveTextAsync("Invalid username or password");
    }

    [Fact]
    public async Task AdminDashboard_LoadsSuccessfully_WhenAuthenticated()
    {
        var page = await _fixture.CreatePageAsync();

        await page.LoginAsAdminAsync();

        var heading = page.Locator("h1");
        await Assertions.Expect(heading).ToContainTextAsync("Admin");

        var managePostsLink = page.Locator("a[href='/admin/posts']");
        await Assertions.Expect(managePostsLink.First).ToBeVisibleAsync(new() { Timeout = 10000 });
    }
}
