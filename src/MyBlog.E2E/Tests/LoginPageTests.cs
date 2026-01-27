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
        await page.FillAsync("input[name='username'], input#username", "invalid");
        await page.FillAsync("input[name='password'], input#password", "invalid");

        // Click submit and wait for the page to reload or error to appear
        // Note: Avoiding WaitForLoadStateAsync(NetworkIdle) as Blazor SignalR connection keeps network active
        await page.ClickAsync("button[type='submit'], input[type='submit']");

        // Wait explicitly for the error message element to appear
        var errorLocator = page.Locator(
            ".error, .error-message, .alert, .alert-danger, .validation-summary, " +
            "[class*='error'], [class*='invalid'], .text-danger, .danger"
        );

        await Assertions.Expect(errorLocator.First).ToBeVisibleAsync();
        await Assertions.Expect(errorLocator.First).ToContainTextAsync("Invalid username or password");
    }

    [Fact]
    public async Task LoginPage_WithValidCredentials_RedirectsToAdmin()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/login");

        // Fill in valid credentials
        await page.FillAsync("input[name='username'], input#username", "admin");
        await page.FillAsync("input[name='password'], input#password", "ChangeMe123!");

        // Submit form
        await page.ClickAsync("button[type='submit'], input[type='submit']");

        // Wait for navigation to complete
        // Using regex pattern to match admin URL safely
        await page.WaitForURLAsync("**/admin**", new PageWaitForURLOptions { Timeout = 60000 });

        // Verify we're on admin page
        Assert.Contains("admin", page.Url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoginPage_AfterLogin_ShowsLogoutButton()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/login");

        // Login first
        await page.FillAsync("input[name='username'], input#username", "admin");
        await page.FillAsync("input[name='password'], input#password", "ChangeMe123!");
        await page.ClickAsync("button[type='submit'], input[type='submit']");
        
        await page.WaitForURLAsync("**/admin**", new PageWaitForURLOptions { Timeout = 60000 });

        // Wait for DOM content to be ready (avoiding NetworkIdle due to SignalR)
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

        // Check for logout element
        var logoutButton = page.Locator(
            "text=/logout/i, text=/sign out/i, button:has-text('Logout'), " +
            "[href='/logout'], form[action*='logout']"
        ).First;

        await Assertions.Expect(logoutButton).ToBeVisibleAsync();
    }

    [Fact]
    public async Task LoginPage_AfterLogin_ShowsLogoutButton_Updated_by_Qwen()
    {
        var page = await _fixture.CreatePageAsync();
        await page.GotoAsync("/login");

        // Login first
        await page.FillAsync("input[name='username'], input#username", "admin");
        await page.FillAsync("input[name='password'], input#password", "ChangeMe123!");
        await page.ClickAsync("button[type='submit'], input[type='submit']");

        try
        {
            // Wait for navigation to complete
            await page.WaitForURLAsync("**/admin**", new PageWaitForURLOptions { Timeout = 90000 });
        }
        catch (TimeoutException ex)
        {
            Console.WriteLine("Test timed out waiting for URL: " + ex.Message);
            throw;
        }

        // Wait for DOM to be ready
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

        // Check for logout element
        var logoutButton = page.Locator(
            "text=/logout/i, text=/sign out/i, button:has-text('Logout'), " +
            "[href='/logout'], form[action*='logout']"
        ).First;

        await Assertions.Expect(logoutButton).ToBeVisibleAsync();
    }
}
