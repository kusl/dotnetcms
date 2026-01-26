// /home/kushal/src/dotnet/MyBlog/src/MyBlog.E2E/Tests/LoginPageTests.cs
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

        var usernameInput = page.Locator("input[name='username'], input#username, input[placeholder*='user' i]");
        var passwordInput = page.Locator("input[name='password'], input#password, input[placeholder*='pass' i]");
        var submitButton = page.Locator("button[type='submit'], input[type='submit'], button:has-text('Login'), button:has-text('Sign In')");

        await Assertions.Expect(usernameInput).ToBeVisibleAsync();
        await Assertions.Expect(passwordInput).ToBeVisibleAsync();
        await Assertions.Expect(submitButton).ToBeVisibleAsync();
    }

    [Fact]
    public async Task LoginPage_WithInvalidCredentials_ShowsError()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/login");

        // Fill in invalid credentials
        await page.FillAsync("input[name='username'], input#username", "invalid");
        await page.FillAsync("input[name='password'], input#password", "invalid");
        await page.ClickAsync("button[type='submit'], input[type='submit']");

        // Wait for ANY visible feedback - error message, alert, or form validation
        var errorElement = page.Locator(
            ".error, .error-message, .alert, .alert-danger, .validation-error, " +
            "[class*='error' i], [class*='invalid' i], .text-danger"
        );
        
        await Assertions.Expect(errorElement.First).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30000 });
    }

    [Fact]
    public async Task LoginPage_WithValidCredentials_RedirectsToAdmin()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/login");

        // Fill in valid credentials
        await page.FillAsync("input[name='username'], input#username", "admin");
        await page.FillAsync("input[name='password'], input#password", "ChangeMe123!");
        await page.ClickAsync("button[type='submit'], input[type='submit']");

        // Wait for navigation to complete
        await page.WaitForURLAsync("**/admin**", new PageWaitForURLOptions { Timeout = 45000 });

        // Verify we're on admin page - look for ANY admin-specific content
        var adminContent = page.Locator(
            "text=/admin dashboard/i, text=/dashboard/i, " +
            "[href='/admin'], [href='/admin/posts'], " +
            "h1, h2:has-text('Admin'), nav >> text=/admin/i"
        );
        
        await Assertions.Expect(adminContent.First).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30000 });
    }

    [Fact]
    public async Task LoginPage_AfterLogin_ShowsLogoutButton()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/login");

        // Fill in valid credentials
        await page.FillAsync("input[name='username'], input#username", "admin");
        await page.FillAsync("input[name='password'], input#password", "ChangeMe123!");
        await page.ClickAsync("button[type='submit'], input[type='submit']");

        // Wait for navigation
        await page.WaitForURLAsync("**/admin**", new PageWaitForURLOptions { Timeout = 45000 });

        // Look for logout in multiple forms - button, link, or form submission
        var logoutElement = page.Locator(
            "text=/logout/i, text=/sign out/i, text=/log out/i, " +
            "button:has-text('Logout'), a:has-text('Logout'), " +
            "[href='/logout'], form[action*='logout'] button, " +
            "button[name='logout'], form button:has-text('Sign Out')"
        );
        
        await Assertions.Expect(logoutElement.First).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30000 });
    }
}
