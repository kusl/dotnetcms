using Microsoft.Playwright;
using Xunit;

namespace MyBlog.E2E.Tests;

/// <summary>
/// E2E tests for the theme switcher (Epic 1: UI/UX).
/// The ThemeSwitcher is a button-based dropdown menu, not a select element.
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

        // The theme switcher is a div with class .theme-switcher containing a button
        var themeSwitcher = page.Locator(".theme-switcher");
        await Assertions.Expect(themeSwitcher).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ThemeSwitcher_ChangesTheme()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        // Wait for Blazor to be fully ready (SignalR connection and JS interop initialized)
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.WaitForTimeoutAsync(1000);

        // Click the theme switcher button to open the menu
        var themeSwitcherBtn = page.Locator(".theme-switcher-btn");
        await themeSwitcherBtn.ClickAsync();

        // Wait for the menu to be visible (CSS transition)
        var themeMenu = page.Locator(".theme-menu.open");
        await Assertions.Expect(themeMenu).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5000 });

        // Click on the "dark" theme option
        var darkOption = page.Locator(".theme-option:has-text('Dark')");
        await darkOption.ClickAsync();

        // Wait a moment for the theme to apply
        await page.WaitForTimeoutAsync(500);

        // Check that theme changed to dark
        var newTheme = await page.EvaluateAsync<string>("document.documentElement.getAttribute('data-theme')");
        Assert.Equal("dark", newTheme);
    }

    [Fact]
    public async Task ThemeSwitcher_HasMultipleOptions()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        // Wait for Blazor to be fully ready (SignalR connection and JS interop initialized)
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.WaitForTimeoutAsync(1000);

        // Click the theme switcher button to open the menu
        var themeSwitcherBtn = page.Locator(".theme-switcher-btn");
        await themeSwitcherBtn.ClickAsync();

        // Wait for the menu to be visible (CSS transition)
        var themeMenu = page.Locator(".theme-menu.open");
        await Assertions.Expect(themeMenu).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5000 });

        // Count the theme options (buttons with class .theme-option)
        var options = page.Locator(".theme-option");
        var count = await options.CountAsync();

        Assert.True(count >= 2, $"Expected at least 2 theme options, got {count}");
    }
}
