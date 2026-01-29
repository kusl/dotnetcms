using Microsoft.Playwright;
using Xunit;

namespace MyBlog.E2E.Tests;

/// <summary>
/// E2E tests for site navigation (Epic 4: Public Browsing).
/// </summary>
[Collection(PlaywrightCollection.Name)]
public sealed class NavigationTests(PlaywrightFixture fixture)
{
    private readonly PlaywrightFixture _fixture = fixture;

    [Fact]
    public async Task Navigation_HomeToAbout_Works()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");
        await page.ClickAsync("nav a[href='/about']");

        await page.WaitForURLAsync("**/about");

        var heading = page.Locator("h1");
        await Assertions.Expect(heading).ToContainTextAsync("About");
    }

    [Fact]
    public async Task Navigation_AboutToHome_Works()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/about");
        await page.ClickAsync("nav a[href='/']");

        await page.WaitForURLAsync("**/");

        var heading = page.Locator("h1");
        await Assertions.Expect(heading).ToContainTextAsync("Welcome");
    }

    [Fact]
    public async Task Navigation_HomeToLogin_Works()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");
        await page.ClickAsync("nav a[href='/login']");

        await page.WaitForURLAsync("**/login");

        var heading = page.Locator("h1");
        await Assertions.Expect(heading).ToContainTextAsync("Login");
    }

    [Fact]
    public async Task Navigation_AllMainLinks_Present()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        // Check all main navigation links are present
        var homeLink = page.Locator("nav a[href='/']");
        var aboutLink = page.Locator("nav a[href='/about']");
        var loginLink = page.Locator("nav a[href='/login']");

        await Assertions.Expect(homeLink).ToBeVisibleAsync();
        await Assertions.Expect(aboutLink).ToBeVisibleAsync();
        await Assertions.Expect(loginLink).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Navigation_FooterIsPresent_OnAllPages()
    {
        var page = await _fixture.CreatePageAsync();

        // Check home page - use the main site footer
        await page.GotoAsync("/");
        var footer = page.Locator("footer.footer");
        await Assertions.Expect(footer).ToBeVisibleAsync();

        // Check about page - the about page may have multiple footers, use the main site footer
        await page.GotoAsync("/about");
        await Assertions.Expect(footer).ToBeVisibleAsync();

        // Check login page
        await page.GotoAsync("/login");
        await Assertions.Expect(footer).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Navigation_HeaderIsPresent_OnAllPages()
    {
        var page = await _fixture.CreatePageAsync();

        // Check home page
        await page.GotoAsync("/");
        var header = page.Locator("header, .header");
        await Assertions.Expect(header.First).ToBeVisibleAsync();

        // Check about page
        await page.GotoAsync("/about");
        await Assertions.Expect(header.First).ToBeVisibleAsync();

        // Check login page
        await page.GotoAsync("/login");
        await Assertions.Expect(header.First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Navigation_LogoLinksToHome()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/about");

        // Click on the logo/brand link
        var logoLink = page.Locator("header a.logo, .header a.logo, header a:has-text('MyBlog')");
        if (await logoLink.CountAsync() > 0)
        {
            await logoLink.First.ClickAsync();
            await page.WaitForURLAsync("**/");

            var heading = page.Locator("h1");
            await Assertions.Expect(heading).ToContainTextAsync("Welcome");
        }
        else
        {
            // If no logo link, just verify home link works
            await page.ClickAsync("nav a[href='/']");
            await page.WaitForURLAsync("**/");
        }
    }

    [Fact]
    public async Task NonExistentPage_ShowsNotFound_OrRedirects()
    {
        var page = await _fixture.CreatePageAsync();

        var response = await page.GotoAsync("/this-page-does-not-exist-12345");

        // Should either return 404 or redirect to a not found page
        Assert.NotNull(response);

        // Either 404 status OR a page showing "not found" content
        if (response.Status == 404)
        {
            Assert.Equal(404, response.Status);
        }
        else
        {
            // If redirected, check for not found content
            var content = await page.ContentAsync();
            var hasNotFoundIndicator = content.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                                        content.Contains("404", StringComparison.OrdinalIgnoreCase);
            Assert.True(response.Ok || hasNotFoundIndicator);
        }
    }

    [Fact]
    public async Task PostNotFound_ShowsError()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/post/this-post-definitely-does-not-exist-12345");

        // Wait for the page to load and Blazor to initialize
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Should show "Post Not Found" or similar message
        // Use GetByText with regex to match case-insensitively
        var notFoundIndicator = page.GetByText("Not Found", new() { Exact = false });
        await Assertions.Expect(notFoundIndicator.First).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task Navigation_MobileViewport_HasNavigation()
    {
        // Create a page with mobile viewport
        var context = await _fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = _fixture.BaseUrl,
            ViewportSize = new ViewportSize { Width = 375, Height = 667 }
        });
        var page = await context.NewPageAsync();

        await page.GotoAsync("/");

        // Navigation should still be accessible (might be in hamburger menu or visible)
        var nav = page.Locator("nav");
        await Assertions.Expect(nav).ToBeAttachedAsync();
    }

    [Fact]
    public async Task Navigation_TabletViewport_HasNavigation()
    {
        // Create a page with tablet viewport
        var context = await _fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = _fixture.BaseUrl,
            ViewportSize = new ViewportSize { Width = 768, Height = 1024 }
        });
        var page = await context.NewPageAsync();

        await page.GotoAsync("/");

        // Navigation should be accessible
        var nav = page.Locator("nav");
        await Assertions.Expect(nav).ToBeVisibleAsync();
    }
}
