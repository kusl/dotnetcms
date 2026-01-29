using Microsoft.Playwright;
using Xunit;

namespace MyBlog.E2E.Tests;

/// <summary>
/// E2E tests for SEO and accessibility features.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public sealed class SeoTests(PlaywrightFixture fixture)
{
    private readonly PlaywrightFixture _fixture = fixture;

    [Fact]
    public async Task HomePage_HasPageTitle()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        var title = await page.TitleAsync();
        Assert.False(string.IsNullOrWhiteSpace(title), "Page should have a title");
    }

    [Fact]
    public async Task AboutPage_HasPageTitle()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/about");

        var title = await page.TitleAsync();
        Assert.Contains("About", title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoginPage_HasPageTitle()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/login");

        var title = await page.TitleAsync();
        Assert.Contains("Login", title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HomePage_HasMainLandmark()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        // Check for main content area with proper role
        var main = page.Locator("main, [role='main']");
        await Assertions.Expect(main.First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task HomePage_HasNavigationLandmark()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        var nav = page.Locator("nav, [role='navigation']");
        await Assertions.Expect(nav.First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task AboutPage_HasProperHeadingHierarchy()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/about");

        // Should have exactly one h1
        var h1Count = await page.Locator("h1").CountAsync();
        Assert.Equal(1, h1Count);
    }

    [Fact]
    public async Task Pages_HaveViewportMeta()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        // Check for viewport meta tag (important for mobile)
        var viewportMeta = page.Locator("meta[name='viewport']");
        await Assertions.Expect(viewportMeta).ToBeAttachedAsync();
    }

    [Fact]
    public async Task Links_HaveVisibleText()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        // Main navigation links should have visible text
        var homeLink = page.Locator("nav a[href='/']");
        var homeLinkText = await homeLink.TextContentAsync();
        Assert.False(string.IsNullOrWhiteSpace(homeLinkText), "Home link should have visible text");
    }

    [Fact]
    public async Task Buttons_HaveAccessibleNames()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        // Theme switcher button should have aria-label
        var themeSwitcherBtn = page.Locator(".theme-switcher-btn");
        if (await themeSwitcherBtn.CountAsync() > 0)
        {
            await Assertions.Expect(themeSwitcherBtn).ToHaveAttributeAsync("aria-label", "Change theme");
        }
    }

    [Fact]
    public async Task LoginForm_HasLabels()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/login");

        // Form inputs should have associated labels
        var usernameLabel = page.Locator("label[for='username']");
        var passwordLabel = page.Locator("label[for='password']");

        await Assertions.Expect(usernameLabel).ToBeVisibleAsync();
        await Assertions.Expect(passwordLabel).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Images_HaveAltText()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/about");

        // If there are any images, they should have alt text
        var images = page.Locator("img");
        var imageCount = await images.CountAsync();

        for (int i = 0; i < imageCount; i++)
        {
            var img = images.Nth(i);
            var alt = await img.GetAttributeAsync("alt");
            // Images should have alt attribute (can be empty for decorative images)
            Assert.NotNull(alt);
        }
    }

    [Fact]
    public async Task Page_HasLanguageAttribute()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        // HTML element should have lang attribute
        var lang = await page.Locator("html").GetAttributeAsync("lang");
        Assert.False(string.IsNullOrWhiteSpace(lang), "HTML should have lang attribute");
    }
}
