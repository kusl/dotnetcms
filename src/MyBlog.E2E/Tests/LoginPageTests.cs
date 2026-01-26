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

        // Fill in invalid credentials
        await page.FillAsync("input#username, input[name='username']", "invalid");
        await page.FillAsync("input#password, input[name='password']", "invalid");
        await page.ClickAsync("button[type='submit']");

        // CRITICAL FIX: Wait for the ACTUAL error element to appear
        // Do NOT wait for URL or DOMContentLoaded - Blazor hydration timing varies in containers
        var errorMessage = page.Locator(".error-message");
        await Assertions.Expect(errorMessage).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 15000 });
        await Assertions.Expect(errorMessage).ToContainTextAsync("Invalid username or password");
    }

    [Fact]
    public async Task LoginPage_WithValidCredentials_RedirectsToAdmin()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/login");

        // Fill in valid credentials
        await page.FillAsync("input#username, input[name='username']", "admin");
        await page.FillAsync("input#password, input[name='password']", "ChangeMe123!");
        await page.ClickAsync("button[type='submit']");

        // CRITICAL FIX: Wait for ADMIN-SPECIFIC content, not URL pattern
        // URL may change before Blazor renders protected content
        var dashboardHeading = page.Locator("h1:has-text('Admin Dashboard')");
        await Assertions.Expect(dashboardHeading).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 45000 });
    }

    [Fact]
    public async Task LoginPage_AfterLogin_ShowsLogoutButton()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/login");

        // Fill in valid credentials
        await page.FillAsync("input#username, input[name='username']", "admin");
        await page.FillAsync("input#password, input[name='password']", "ChangeMe123!");
        await page.ClickAsync("button[type='submit']");

        // CRITICAL FIX: Wait for logout button visibility directly
        // Avoids race condition between navigation completion and Blazor rendering
        var logoutButton = page.Locator("button:has-text('Logout'), form[action='/logout'] button");
        await Assertions.Expect(logoutButton).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 45000 });
    }
}
