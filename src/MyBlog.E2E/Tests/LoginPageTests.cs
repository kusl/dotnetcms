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
public async Task EnsureAdminUserExists()
{
    // This ensures the admin user is created before other tests run
    using var client = new HttpClient { BaseAddress = new Uri(_fixture.BaseUrl) };
    var response = await client.GetAsync("/login", TestContext.Current.CancellationToken);
    Assert.True(response.StatusCode == System.Net.HttpStatusCode.OK);
}

[Obsolete]
[Fact]
public async Task LoginPage_WithValidCredentials_RedirectsToAdmin()
{
    var page = await _fixture.CreatePageAsync();
    await page.GotoAsync("/login");

    await page.FillAsync("input[name='username']", "admin");
    await page.FillAsync("input[name='password']", "ChangeMe123!");

    // FIX: Use RunAndWaitForNavigationAsync to ensure form submission completes
    await page.RunAndWaitForNavigationAsync(async () =>
    {
        await page.ClickAsync("button[type='submit']");
    }, new PageRunAndWaitForNavigationOptions 
    { 
        UrlRegex = new Regex("/admin|/login.*error") 
    });

    // Now assert the URL
    await Assertions.Expect(page).ToHaveURLAsync(new Regex("/admin"));
}

[Obsolete]
[Fact]
public async Task LoginPage_WithInvalidCredentials_ShowsError()
{
    var page = await _fixture.CreatePageAsync();
    await page.GotoAsync("/login");

    await page.FillAsync("input[name='username']", "invalid");
    await page.FillAsync("input[name='password']", "invalid");

    // FIX: Wait for navigation to complete (even if it redirects back to login)
    await page.RunAndWaitForNavigationAsync(async () =>
    {
        await page.ClickAsync("button[type='submit']");
    });

    // Wait specifically for the error query parameter to appear
    await Assertions.Expect(page).ToHaveURLAsync(new Regex("/login.*error=invalid"));

    // Now check for the error message
    var errorLocator = page.Locator(".error-message");
    await Assertions.Expect(errorLocator).ToBeVisibleAsync();
    await Assertions.Expect(errorLocator).ToContainTextAsync("Invalid username or password");
}

[Obsolete]
[Fact]
public async Task LoginPage_AfterLogin_ShowsLogoutButton()
{
    var page = await _fixture.CreatePageAsync();
    await page.GotoAsync("/login");

    await page.FillAsync("input[name='username']", "admin");
    await page.FillAsync("input[name='password']", "ChangeMe123!");

    // FIX: Explicitly wait for navigation to admin page
    await page.RunAndWaitForNavigationAsync(async () =>
    {
        await page.ClickAsync("button[type='submit']");
    }, new PageRunAndWaitForNavigationOptions { UrlRegex = new Regex("/admin") });

    await Assertions.Expect(page).ToHaveURLAsync(new Regex("/admin"));
    await Assertions.Expect(page.Locator("h1")).ToContainTextAsync("Dashboard");
    
    var logoutButton = page.Locator("form[action='/logout'] button");
    await Assertions.Expect(logoutButton).ToBeVisibleAsync();
}

}
