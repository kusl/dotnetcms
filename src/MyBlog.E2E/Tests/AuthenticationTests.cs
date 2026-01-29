using Microsoft.Playwright;
using Xunit;

namespace MyBlog.E2E.Tests;

/// <summary>
/// E2E tests for authentication flows (Epic 1: Authentication Journey).
/// Tests the complete authentication lifecycle including login, logout, and protected routes.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public sealed class AuthenticationTests(PlaywrightFixture fixture)
{
    private readonly PlaywrightFixture _fixture = fixture;

    [Fact]
    public async Task ProtectedRoute_RedirectsToLogin_WhenNotAuthenticated()
    {
        var page = await _fixture.CreatePageAsync();

        // Try to access admin page directly
        await page.GotoAsync("/admin");

        // Should be redirected to login
        await page.WaitForURLAsync("**/login**", new() { Timeout = 10000 });

        // Verify we're on the login page
        var heading = page.Locator("h1");
        await Assertions.Expect(heading).ToContainTextAsync("Login");
    }

    [Fact]
    public async Task ProtectedRoute_IncludesReturnUrl_WhenRedirected()
    {
        var page = await _fixture.CreatePageAsync();

        // Try to access admin posts page directly
        await page.GotoAsync("/admin/posts");

        // Should be redirected to login with return URL
        await page.WaitForURLAsync("**/login**", new() { Timeout = 10000 });

        // Check the URL contains returnUrl parameter
        var url = page.Url;
        Assert.Contains("returnUrl", url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_WithDefaultCredentials_Succeeds()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/login");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Fill in default admin credentials
        await page.FillAsync("input[name='username']", "admin");
        await page.FillAsync("input[name='password']", "ChangeMe123!");

        // Submit the form and wait for navigation in parallel
        // This is the proper pattern for traditional HTML form submissions
        await Task.WhenAll(
            page.WaitForURLAsync("**/admin**", new() { Timeout = 30000 }),
            page.Locator("button[type='submit']").ClickAsync()
        );

        // Verify we're authenticated
        var adminHeading = page.Locator("h1");
        await Assertions.Expect(adminHeading).ToContainTextAsync("Admin", new() { Timeout = 10000 });
    }

    [Fact]
    public async Task Login_ShowsAdminLink_WhenAuthenticated()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/login");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Fill in credentials and submit
        await page.FillAsync("input[name='username']", "admin");
        await page.FillAsync("input[name='password']", "ChangeMe123!");

        // Submit and wait for navigation
        await Task.WhenAll(
            page.WaitForURLAsync("**/admin**", new() { Timeout = 30000 }),
            page.Locator("button[type='submit']").ClickAsync()
        );

        // Navigate to home page
        await page.GotoAsync("/");

        // Admin link should be visible
        var adminLink = page.Locator("nav a[href='/admin']");
        await Assertions.Expect(adminLink).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task Logout_RedirectsToHome()
    {
        var page = await _fixture.CreatePageAsync();

        // First, login
        await page.GotoAsync("/login");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await page.FillAsync("input[name='username']", "admin");
        await page.FillAsync("input[name='password']", "ChangeMe123!");

        await Task.WhenAll(
            page.WaitForURLAsync("**/admin**", new() { Timeout = 30000 }),
            page.Locator("button[type='submit']").ClickAsync()
        );

        // Find and click the logout button
        var logoutButton = page.Locator("button:has-text('Logout')").Or(page.Locator("form[action='/logout'] button"));
        await Assertions.Expect(logoutButton.First).ToBeVisibleAsync(new() { Timeout = 10000 });

        // Click logout and wait for navigation
        await Task.WhenAll(
            page.WaitForURLAsync("**/", new() { Timeout = 10000 }),
            logoutButton.First.ClickAsync()
        );

        // Login link should be visible instead of Admin link
        var loginLink = page.Locator("nav a[href='/login']");
        await Assertions.Expect(loginLink).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task Logout_RemovesAdminLink()
    {
        var page = await _fixture.CreatePageAsync();

        // First, login
        await page.GotoAsync("/login");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await page.FillAsync("input[name='username']", "admin");
        await page.FillAsync("input[name='password']", "ChangeMe123!");

        await Task.WhenAll(
            page.WaitForURLAsync("**/admin**", new() { Timeout = 30000 }),
            page.Locator("button[type='submit']").ClickAsync()
        );

        // Click logout
        var logoutButton = page.Locator("button:has-text('Logout')").Or(page.Locator("form[action='/logout'] button"));

        await Task.WhenAll(
            page.WaitForURLAsync("**/", new() { Timeout = 10000 }),
            logoutButton.First.ClickAsync()
        );

        // Admin link should not be visible
        var adminLink = page.Locator("nav a[href='/admin']");
        await Assertions.Expect(adminLink).Not.ToBeVisibleAsync(new() { Timeout = 5000 });
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ShowsError()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/login");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Fill in invalid credentials
        await page.FillAsync("input[name='username']", "admin");
        await page.FillAsync("input[name='password']", "WrongPassword!");

        // Submit the form and wait for it to return to login page with error
        await Task.WhenAll(
            page.WaitForURLAsync("**/login**", new() { Timeout = 30000 }),
            page.Locator("button[type='submit']").ClickAsync()
        );

        // Either URL has error param or page shows error message
        var url = page.Url;
        var errorMessage = page.Locator(".error-message");

        var hasErrorInUrl = url.Contains("error=");
        var hasErrorMessage = await errorMessage.CountAsync() > 0;

        Assert.True(hasErrorInUrl || hasErrorMessage, "Expected error indication after invalid login");
    }

    [Fact]
    public async Task Login_WithInvalidUsername_ShowsError()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/login");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Fill in non-existent user
        await page.FillAsync("input[name='username']", "nonexistentuser");
        await page.FillAsync("input[name='password']", "SomePassword123!");

        // Submit the form
        await Task.WhenAll(
            page.WaitForURLAsync("**/login**", new() { Timeout = 30000 }),
            page.Locator("button[type='submit']").ClickAsync()
        );

        // Check for error
        var url = page.Url;
        var errorMessage = page.Locator(".error-message");

        var hasErrorInUrl = url.Contains("error=");
        var hasErrorMessage = await errorMessage.CountAsync() > 0;

        Assert.True(hasErrorInUrl || hasErrorMessage, "Expected error indication after invalid login");
    }

    [Fact]
    public async Task AdminDashboard_LoadsSuccessfully_WhenAuthenticated()
    {
        var page = await _fixture.CreatePageAsync();

        // Login first
        await page.GotoAsync("/login");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await page.FillAsync("input[name='username']", "admin");
        await page.FillAsync("input[name='password']", "ChangeMe123!");

        await Task.WhenAll(
            page.WaitForURLAsync("**/admin**", new() { Timeout = 30000 }),
            page.Locator("button[type='submit']").ClickAsync()
        );

        // Verify dashboard content
        var heading = page.Locator("h1");
        await Assertions.Expect(heading).ToContainTextAsync("Admin");

        // Should have links to manage posts
        var managePostsLink = page.Locator("a[href='/admin/posts']").Or(page.GetByText("Posts"));
        await Assertions.Expect(managePostsLink.First).ToBeVisibleAsync(new() { Timeout = 10000 });
    }
}
