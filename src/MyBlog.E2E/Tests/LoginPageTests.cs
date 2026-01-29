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
    public async Task LoginPage_DisplaysLoginHeading()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/login");

        var heading = page.Locator("h1");
        await Assertions.Expect(heading).ToBeVisibleAsync();
        await Assertions.Expect(heading).ToContainTextAsync("Login");
    }

    [Fact]
    public async Task LoginPage_HasUsernameField()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/login");

        var usernameInput = page.Locator("input[name='username']");
        await Assertions.Expect(usernameInput).ToBeVisibleAsync();
    }

    [Fact]
    public async Task LoginPage_HasPasswordField()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/login");

        var passwordInput = page.Locator("input[name='password']");
        await Assertions.Expect(passwordInput).ToBeVisibleAsync();
    }

    [Fact]
    public async Task LoginPage_HasSubmitButton()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/login");

        var submitButton = page.Locator("button[type='submit']");
        await Assertions.Expect(submitButton).ToBeVisibleAsync();
    }

    [Fact]
    public async Task LoginPage_FieldsAreRequired()
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

        // Submit the form and wait for navigation back to login with error
        await Task.WhenAll(
            page.WaitForURLAsync("**/login*", new() { Timeout = 30000 }),
            page.Locator("button[type='submit']").ClickAsync()
        );

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

        // Submit form and wait for redirect
        await Task.WhenAll(
            page.WaitForURLAsync("**/admin**", new() { Timeout = 30000 }),
            page.Locator("button[type='submit']").ClickAsync()
        );

        // Verify we're on the admin page
        var heading = page.Locator("h1");
        await Assertions.Expect(heading).ToContainTextAsync("Admin");
    }
}
