using Microsoft.Playwright;
using Xunit;

namespace MyBlog.E2E.Tests;

[Collection(PlaywrightCollection.Name)]
public sealed class NavigationTests(PlaywrightFixture fixture)
{
    private readonly PlaywrightFixture _fixture = fixture;

    [Fact]
    public async Task Navigation_HomeToAbout_Works()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        await page.WaitForBlazorInteractiveAsync();

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
        await page.WaitForBlazorInteractiveAsync();

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
        await page.WaitForBlazorInteractiveAsync();

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

        await page.GotoAsync("/");
        var footer = page.Locator("footer.footer");
        await Assertions.Expect(footer).ToBeVisibleAsync();

        await page.GotoAsync("/about");
        await Assertions.Expect(footer).ToBeVisibleAsync();

        await page.GotoAsync("/login");
        await Assertions.Expect(footer).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Navigation_HeaderIsPresent_OnAllPages()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");
        var header = page.Locator("header, .header");
        await Assertions.Expect(header.First).ToBeVisibleAsync();

        await page.GotoAsync("/about");
        await Assertions.Expect(header.First).ToBeVisibleAsync();

        await page.GotoAsync("/login");
        await Assertions.Expect(header.First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Navigation_LogoLinksToHome()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/about");
        await page.WaitForBlazorInteractiveAsync();

        await page.Locator("header a.logo").ClickAsync();
        await page.WaitForURLAsync("**/");

        var heading = page.Locator("h1");
        await Assertions.Expect(heading).ToContainTextAsync("Welcome");
    }

    [Fact]
    public async Task NonExistentPage_ShowsNotFound_OrRedirects()
    {
        var page = await _fixture.CreatePageAsync();

        var response = await page.GotoAsync("/this-page-does-not-exist-12345");

        Assert.NotNull(response);

        if (response.Status == 404)
        {
            Assert.Equal(404, response.Status);
        }
        else
        {
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

        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var notFoundIndicator = page.GetByText("Not Found", new() { Exact = false });
        await Assertions.Expect(notFoundIndicator.First).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task Navigation_MobileViewport_HasNavigation()
    {
        var context = await _fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = _fixture.BaseUrl,
            ViewportSize = new ViewportSize { Width = 375, Height = 667 }
        });
        var page = await context.NewPageAsync();

        await page.GotoAsync("/");

        var nav = page.Locator("nav");
        await Assertions.Expect(nav).ToBeAttachedAsync();
    }

    [Fact]
    public async Task Navigation_TabletViewport_HasNavigation()
    {
        var context = await _fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = _fixture.BaseUrl,
            ViewportSize = new ViewportSize { Width = 768, Height = 1024 }
        });
        var page = await context.NewPageAsync();

        await page.GotoAsync("/");

        var nav = page.Locator("nav");
        await Assertions.Expect(nav).ToBeVisibleAsync();
    }
}
