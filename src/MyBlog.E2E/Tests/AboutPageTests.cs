using Microsoft.Playwright;
using Xunit;

namespace MyBlog.E2E.Tests;

/// <summary>
/// E2E tests for the about page (Epic 4: Public Browsing).
/// </summary>
[Collection(PlaywrightCollection.Name)]
public sealed class AboutPageTests(PlaywrightFixture fixture)
{
    private readonly PlaywrightFixture _fixture = fixture;

    [Fact]
    public async Task AboutPage_LoadsSuccessfully()
    {
        var page = await _fixture.CreatePageAsync();

        var response = await page.GotoAsync("/about");

        Assert.NotNull(response);
        Assert.True(response.Ok, $"Expected OK response, got {response.Status}");
    }

    [Fact]
    public async Task AboutPage_DisplaysAboutHeading()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/about");

        var heading = page.Locator("h1");
        await Assertions.Expect(heading).ToBeVisibleAsync();
        await Assertions.Expect(heading).ToContainTextAsync("About");
    }

    [Fact]
    public async Task AboutPage_DisplaysArchitectureSection()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/about");

        var architectureSection = page.Locator("text=Architecture");
        await Assertions.Expect(architectureSection.First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task AboutPage_DisplaysTechnologyStack()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/about");

        // The about page should mention .NET and Blazor
        var content = await page.ContentAsync();
        Assert.Contains(".NET", content);
        Assert.Contains("Blazor", content);
    }

    [Fact]
    public async Task AboutPage_HasReaderBadge()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/about");

        // Wait for SignalR connection and reader badge
        var readerBadge = page.Locator(".reader-badge, .reader-info");
        await Assertions.Expect(readerBadge.First).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task AboutPage_HasNavigationBackToHome()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/about");

        // Should have navigation links
        var homeLink = page.Locator("nav a[href='/']");
        await Assertions.Expect(homeLink).ToBeVisibleAsync();
    }

    [Fact]
    public async Task AboutPage_DisplaysOverviewSection()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/about");

        var overviewSection = page.Locator("text=Overview");
        await Assertions.Expect(overviewSection.First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task AboutPage_MentionsCleanArchitecture()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/about");

        var content = await page.ContentAsync();
        Assert.Contains("Clean Architecture", content);
    }
}
