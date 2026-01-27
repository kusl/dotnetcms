using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace MyBlog.E2E.Tests;

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

        // Use Web Assertions to wait for visibility
        await Assertions.Expect(page.Locator("input[name='username']")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("input[name='password']")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("button[type='submit']")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task LoginPage_WithInvalidCredentials_ShowsError()
    {
        var page = await _fixture.CreatePageAsync();
        await page.GotoAsync("/login");

        await page.FillAsync("input[name='username']", "invalid");
        await page.FillAsync("input[name='password']", "invalid");

        await page.ClickAsync("button[type='submit']");

        // Web Assertion: This automatically waits for the error message to appear
        // It handles the postback and re-render implicitly.
        var errorLocator = page.Locator(".error-message");
        await Assertions.Expect(errorLocator).ToBeVisibleAsync();
        await Assertions.Expect(errorLocator).ToContainTextAsync("Invalid username or password");
    }

    [Fact]
    public async Task LoginPage_WithValidCredentials_RedirectsToAdmin()
    {
        var page = await _fixture.CreatePageAsync();
        await page.GotoAsync("/login");

        await page.FillAsync("input[name='username']", "admin");
        await page.FillAsync("input[name='password']", "ChangeMe123!");

        await page.ClickAsync("button[type='submit']");

        // KEY FIX: Use Expect(page).ToHaveURLAsync
        // This replaces WaitForURLAsync. It retries repeatedly until the URL matches
        // the regex or the timeout is reached. It implies navigation is complete.
        await Assertions.Expect(page).ToHaveURLAsync(new Regex("/admin"));
    }

    [Fact]
    public async Task LoginPage_AfterLogin_ShowsLogoutButton()
    {
        var page = await _fixture.CreatePageAsync();
        await page.GotoAsync("/login");

        await page.FillAsync("input[name='username']", "admin");
        await page.FillAsync("input[name='password']", "ChangeMe123!");

        await page.ClickAsync("button[type='submit']");

        // 1. Guard Assertion: Verify we landed on the right URL first.
        // This ensures the POST succeeded and redirect happened.
        await Assertions.Expect(page).ToHaveURLAsync(new Regex("/admin"));

        // 2. State Assertion: Verify Blazor has hydrated and shows the Dashboard header.
        // In your Dashboard.razor, there is an <h1>Admin Dashboard</h1>.
        // Waiting for this ensures the main content area is ready.
        await Assertions.Expect(page.Locator("h1")).ToContainTextAsync("Dashboard");

        // 3. Auth Assertion: Verify the Logout button is visible.
        // In MainLayout.razor, this button is inside <Authorized>, so its presence
        // proves authentication state is resolved.
        var logoutButton = page.Locator("form[action='/logout'] button");
        await Assertions.Expect(logoutButton).ToBeVisibleAsync();
    }
}
