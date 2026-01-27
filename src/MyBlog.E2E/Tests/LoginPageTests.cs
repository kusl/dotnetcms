// /home/kushal/src/dotnet/MyBlog/src/MyBlog.E2E/Tests/LoginPageTests.cs

using System.Text.RegularExpressions;
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

        await page.FillAsync("input[name='username']", "invalid");
        await page.FillAsync("input[name='password']", "invalid");

        // Submit and wait for the error message to appear
        await page.ClickAsync("button[type='submit']");

        // Target the actual class used in Login.razor
        var errorLocator = page.Locator(".error-message");
        await Assertions.Expect(errorLocator).ToBeVisibleAsync();
    }

    [Obsolete]
    [Fact]
    public async Task LoginPage_WithValidCredentials_RedirectsToAdmin()
    {
        var page = await _fixture.CreatePageAsync();
        await page.GotoAsync("/login");

        await page.FillAsync("input[name='username']", "admin");
        await page.FillAsync("input[name='password']", "ChangeMe123!");

        // Use RunAndWaitForNavigationAsync to capture the redirect to /admin
        await page.RunAndWaitForNavigationAsync(async () =>
        {
            await page.ClickAsync("button[type='submit']");
        }, new PageRunAndWaitForNavigationOptions { UrlString = "**/admin", WaitUntil = WaitUntilState.DOMContentLoaded });

        Assert.Contains("/admin", page.Url);
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

        // Wait for navigation
        await page.WaitForURLAsync("**/admin**", new PageWaitForURLOptions { Timeout = 90000 });
        var logoutButton = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { NameRegex = new Regex("logout|sign out", RegexOptions.IgnoreCase) })
            .Or(page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { NameRegex = new Regex("logout|sign out", RegexOptions.IgnoreCase) }))
            .First;
        await Assertions.Expect(logoutButton).ToBeVisibleAsync();
    }
}
