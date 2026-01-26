using Microsoft.Playwright;
using Xunit;

namespace MyBlog.E2E.Tests;

/// <summary>
/// E2E tests for authentication (Epic 1: Authentication).
/// The login page uses a standard HTML form that posts to /login minimal API endpoint.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public sealed class LoginPageTests(PlaywrightFixture fixture)
{
    private readonly PlaywrightFixture _fixture = fixture;

    [Fact]
    public async Task LoginPage_LoadsSuccessfully()
    {
        var page = await _fixture.CreatePageAsync();

        var response = await page.GotoAsync("/login");

        Assert.NotNull(response);
        Assert.True(response.Ok, $"Expected OK response, got {response.Status}");
    }

    [Fact]
    public async Task LoginPage_DisplaysLoginForm()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/login");

        var usernameInput = page.Locator("input#username, input[name='username']");
        var passwordInput = page.Locator("input#password, input[name='password']");
        var submitButton = page.Locator("button[type='submit']");

        await Assertions.Expect(usernameInput).ToBeVisibleAsync();
        await Assertions.Expect(passwordInput).ToBeVisibleAsync();
        await Assertions.Expect(submitButton).ToBeVisibleAsync();
    }

    [Fact]
    public async Task LoginPage_WithInvalidCredentials_ShowsError()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/login");

        // Wait for page to be fully loaded
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

        // Fill in invalid credentials
        await page.FillAsync("input#username, input[name='username']", "invalid");
        await page.FillAsync("input#password, input[name='password']", "invalid");

        // Submit form and wait for navigation (POST redirects to /login?error=invalid)
        await page.RunAndWaitForNavigationAsync(async () =>
        {
            await page.ClickAsync("button[type='submit']");
        }, new PageRunAndWaitForNavigationOptions
        {
            UrlString = "**/login**",
            WaitUntil = WaitUntilState.Load,
            Timeout = 15000
        });

        // Wait for page to render
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

        // Verify error message is displayed
        var errorMessage = page.Locator(".error-message");
        await Assertions.Expect(errorMessage).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5000 });
        await Assertions.Expect(errorMessage).ToContainTextAsync("Invalid username or password");
    }

    [Fact]
    public async Task LoginPage_WithValidCredentials_RedirectsToAdmin()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/login");

        // Wait for page to be fully loaded
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

        // Fill in valid credentials
        await page.FillAsync("input#username, input[name='username']", "admin");
        await page.FillAsync("input#password, input[name='password']", "ChangeMe123!");

        // Submit form and wait for navigation to admin
        await page.RunAndWaitForNavigationAsync(async () =>
        {
            await page.ClickAsync("button[type='submit']");
        }, new PageRunAndWaitForNavigationOptions
        {
            UrlString = "**/admin**",
            WaitUntil = WaitUntilState.Load,
            Timeout = 15000
        });

        // Verify we're on an admin page
        var url = page.Url;
        Assert.Contains("admin", url);
    }

    [Fact]
    public async Task LoginPage_AfterLogin_ShowsLogoutButton()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/login");

        // Wait for page to be fully loaded
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

        // Fill in valid credentials
        await page.FillAsync("input#username, input[name='username']", "admin");
        await page.FillAsync("input#password, input[name='password']", "ChangeMe123!");

        // Submit form and wait for navigation to admin
        await page.RunAndWaitForNavigationAsync(async () =>
        {
            await page.ClickAsync("button[type='submit']");
        }, new PageRunAndWaitForNavigationOptions
        {
            UrlString = "**/admin**",
            WaitUntil = WaitUntilState.Load,
            Timeout = 15000
        });

        // Wait for page to fully render
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

        // Logout button should now be visible
        var logoutButton = page.Locator("button:has-text('Logout'), form[action='/logout'] button");
        await Assertions.Expect(logoutButton).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5000 });
    }
}
