using Microsoft.Playwright;
using Xunit;

namespace MyBlog.E2E.Tests;

/// <summary>
/// E2E tests for the about page (Epic 1: Public Content Viewing).
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
        await Assertions.Expect(readerBadge.First).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10000 });
    }
}
