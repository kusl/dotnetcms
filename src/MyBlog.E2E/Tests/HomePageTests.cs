using Microsoft.Playwright;
using Xunit;

namespace MyBlog.E2E.Tests;

/// <summary>
/// E2E tests for the homepage (Epic 1: Public Content Viewing).
/// </summary>
[Collection(PlaywrightCollection.Name)]
public sealed class HomePageTests(PlaywrightFixture fixture)
{
    private readonly PlaywrightFixture _fixture = fixture;

    [Fact]
    public async Task HomePage_LoadsSuccessfully()
    {
        var page = await _fixture.CreatePageAsync();

        var response = await page.GotoAsync("/");

        Assert.NotNull(response);
        Assert.True(response.Ok, $"Expected OK response, got {response.Status}");
    }

    [Fact]
    public async Task HomePage_DisplaysWelcomeHeading()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        var heading = page.Locator("h1");
        await Assertions.Expect(heading).ToBeVisibleAsync();
        await Assertions.Expect(heading).ToContainTextAsync("Welcome");
    }

    [Fact]
    public async Task HomePage_HasNavigationLinks()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        // Check for essential navigation links
        var homeLink = page.Locator("nav a[href='/']");
        var aboutLink = page.Locator("nav a[href='/about']");
        var loginLink = page.Locator("nav a[href='/login']");

        await Assertions.Expect(homeLink).ToBeVisibleAsync();
        await Assertions.Expect(aboutLink).ToBeVisibleAsync();
        await Assertions.Expect(loginLink).ToBeVisibleAsync();
    }

    [Fact]
    public async Task HomePage_HasFooter()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        var footer = page.Locator("footer");
        await Assertions.Expect(footer).ToBeVisibleAsync();
    }

    [Fact]
    public async Task HomePage_NavigationToAbout_Works()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        await page.ClickAsync("nav a[href='/about']");

        await page.WaitForURLAsync("**/about");
        var heading = page.Locator("h1");
        await Assertions.Expect(heading).ToContainTextAsync("About");
    }
}
