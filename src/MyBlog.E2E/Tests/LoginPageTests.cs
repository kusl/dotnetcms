using Microsoft.Playwright;
using Xunit;

namespace MyBlog.E2E.Tests;

/// <summary>
/// E2E tests for the login page (Epic 1: Authentication).
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

        // Use Web Assertions to wait for visibility
        await Assertions.Expect(page.Locator("input[name='username']")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("input[name='password']")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("button[type='submit']")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task LoginPage_DisplaysLoginHeading()
    {
        var page = await _fixture.CreatePageAsync();
        await page.GotoAsync("/login");

        var heading = page.Locator("h1");
        await Assertions.Expect(heading).ToBeVisibleAsync();
        await Assertions.Expect(heading).ToContainTextAsync("Login");
    }

    [Fact]
    public async Task LoginPage_HasRequiredFieldAttributes()
    {
        var page = await _fixture.CreatePageAsync();
        await page.GotoAsync("/login");

        var usernameInput = page.Locator("input[name='username']");
        var passwordInput = page.Locator("input[name='password']");

        // Check required attributes
        await Assertions.Expect(usernameInput).ToHaveAttributeAsync("required", "");
        await Assertions.Expect(passwordInput).ToHaveAttributeAsync("required", "");
    }

    [Fact]
    public async Task LoginPage_PasswordFieldIsTypePassword()
    {
        var page = await _fixture.CreatePageAsync();
        await page.GotoAsync("/login");

        var passwordInput = page.Locator("input[name='password']");
        await Assertions.Expect(passwordInput).ToHaveAttributeAsync("type", "password");
    }

    [Fact]
    public async Task LoginPage_HasAntiforgeryToken()
    {
        var page = await _fixture.CreatePageAsync();
        await page.GotoAsync("/login");

        // Blazor forms should have an antiforgery token
        var antiforgeryInput = page.Locator("input[name='__RequestVerificationToken']");
        await Assertions.Expect(antiforgeryInput).ToBeAttachedAsync();
    }

    [Fact]
    public async Task LoginPage_FormHasCorrectAction()
    {
        var page = await _fixture.CreatePageAsync();
        await page.GotoAsync("/login");

        var form = page.Locator("form[action='/account/login']");
        await Assertions.Expect(form).ToBeVisibleAsync();
    }

    [Fact]
    public async Task LoginPage_InvalidCredentials_ShowsError()
    {
        var page = await _fixture.CreatePageAsync();
        await page.GotoAsync("/login");

        // Wait for form to be ready
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Fill in invalid credentials
        await page.FillAsync("input[name='username']", "invaliduser");
        await page.FillAsync("input[name='password']", "invalidpassword");

        // Submit the form and wait for navigation
        await page.Locator("button[type='submit']").ClickAsync();

        // Wait for the error message to appear (redirects back to login with error)
        await page.WaitForURLAsync("**/login*");

        // Check for error indicator in URL or on page
        var url = page.Url;
        var hasErrorParam = url.Contains("error=");
        var errorMessage = page.Locator(".error-message");
        var hasErrorMessage = await errorMessage.CountAsync() > 0;

        Assert.True(hasErrorParam || hasErrorMessage, "Expected error indication after invalid login");
    }

    [Fact]
    public async Task LoginPage_SuccessfulLogin_RedirectsToAdmin()
    {
        var page = await _fixture.CreatePageAsync();
        await page.GotoAsync("/login");

        // Wait for form to be ready
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Fill in default admin credentials
        await page.FillAsync("input[name='username']", "admin");
        await page.FillAsync("input[name='password']", "ChangeMe123!");

        // Submit the form
        await page.Locator("button[type='submit']").ClickAsync();

        // Wait for redirect to admin dashboard
        await page.WaitForURLAsync("**/admin**", new() { Timeout = 15000 });

        // Verify we're on the admin page
        var adminHeading = page.Locator("h1");
        await Assertions.Expect(adminHeading).ToContainTextAsync("Admin", new() { Timeout = 10000 });
    }

    [Fact]
    public async Task LoginPage_WithReturnUrl_PreservesReturnUrl()
    {
        var page = await _fixture.CreatePageAsync();

        // Navigate to login with a return URL
        await page.GotoAsync("/login?returnUrl=%2Fadmin%2Fposts");

        // Check that the hidden input has the return URL
        var returnUrlInput = page.Locator("input[name='returnUrl']");
        await Assertions.Expect(returnUrlInput).ToHaveValueAsync("/admin/posts");
    }

    [Fact]
    public async Task LoginPage_HasNavigationLinks()
    {
        var page = await _fixture.CreatePageAsync();
        await page.GotoAsync("/login");

        // Should still have navigation to home and about
        var homeLink = page.Locator("nav a[href='/']");
        var aboutLink = page.Locator("nav a[href='/about']");

        await Assertions.Expect(homeLink).ToBeVisibleAsync();
        await Assertions.Expect(aboutLink).ToBeVisibleAsync();
    }
}
