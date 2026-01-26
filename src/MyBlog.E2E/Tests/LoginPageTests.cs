using Microsoft.Playwright;
using Xunit;

namespace MyBlog.E2E.Tests;

/// <summary>
/// E2E tests for authentication (Epic 1: Authentication).
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

        await page.FillAsync("input#username, input[name='username']", "invalid");
        await page.FillAsync("input#password, input[name='password']", "invalid");
        await page.ClickAsync("button[type='submit']");

        // Wait for error message to appear
        await page.WaitForSelectorAsync(".error-message, .error", new() { Timeout = 5000 });

        var errorMessage = page.Locator(".error-message, .error");
        await Assertions.Expect(errorMessage).ToBeVisibleAsync();
    }

    [Fact]
    public async Task LoginPage_WithValidCredentials_RedirectsToAdmin()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/login");

        // Use default credentials
        await page.FillAsync("input#username, input[name='username']", "admin");
        await page.FillAsync("input#password, input[name='password']", "ChangeMe123!");
        await page.ClickAsync("button[type='submit']");

        // Wait for redirect to admin
        await page.WaitForURLAsync("**/admin**", new() { Timeout = 10000 });

        // Verify we're on an admin page
        var url = page.Url;
        Assert.Contains("admin", url);
    }

    [Fact]
    public async Task LoginPage_AfterLogin_ShowsLogoutButton()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/login");

        await page.FillAsync("input#username, input[name='username']", "admin");
        await page.FillAsync("input#password, input[name='password']", "ChangeMe123!");
        await page.ClickAsync("button[type='submit']");

        await page.WaitForURLAsync("**/admin**", new() { Timeout = 10000 });

        // Logout button should now be visible
        var logoutButton = page.Locator("button:has-text('Logout'), form[action='/logout'] button");
        await Assertions.Expect(logoutButton).ToBeVisibleAsync();
    }
}
