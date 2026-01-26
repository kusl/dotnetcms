using Microsoft.Playwright;
using Xunit;

namespace MyBlog.E2E.Tests;

/// <summary>
/// E2E tests for the theme switcher (Epic 1: UI/UX).
/// </summary>
[Collection(PlaywrightCollection.Name)]
public sealed class ThemeSwitcherTests(PlaywrightFixture fixture)
{
    private readonly PlaywrightFixture _fixture = fixture;

    [Fact]
    public async Task ThemeSwitcher_IsVisibleOnPage()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        var themeSwitcher = page.Locator(".theme-switcher, select[aria-label*='theme' i], select:has(option[value='dark'])");
        await Assertions.Expect(themeSwitcher.First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ThemeSwitcher_ChangesTheme()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        // Get initial theme
        var initialTheme = await page.EvaluateAsync<string>("document.documentElement.getAttribute('data-theme')");

        // Find and click the theme switcher
        var themeSwitcher = page.Locator(".theme-switcher select, select:has(option[value='dark'])");
        await themeSwitcher.First.SelectOptionAsync("dark");

        // Wait a moment for the theme to apply
        await page.WaitForTimeoutAsync(500);

        // Check that theme changed
        var newTheme = await page.EvaluateAsync<string>("document.documentElement.getAttribute('data-theme')");
        Assert.Equal("dark", newTheme);
    }

    [Fact]
    public async Task ThemeSwitcher_HasMultipleOptions()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        var themeSwitcher = page.Locator(".theme-switcher select, select:has(option[value='dark'])");
        var options = themeSwitcher.First.Locator("option");

        var count = await options.CountAsync();
        Assert.True(count >= 2, $"Expected at least 2 theme options, got {count}");
    }
}
