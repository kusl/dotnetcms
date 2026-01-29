using Microsoft.Playwright;
using Xunit;

namespace MyBlog.E2E.Tests;

/// <summary>
/// E2E tests for the theme switcher (Epic 4: UI/UX).
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
    public async Task ThemeSwitcher_ButtonIsClickable()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        // Wait for Blazor to be fully ready
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var themeSwitcherBtn = page.Locator(".theme-switcher-btn");
        await Assertions.Expect(themeSwitcherBtn).ToBeVisibleAsync();
        await Assertions.Expect(themeSwitcherBtn).ToBeEnabledAsync();
    }

    [Fact]
    public async Task ThemeSwitcher_OpensMenuOnClick()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        // Wait for Blazor to be fully ready (SignalR connection and JS interop initialized)
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Wait for theme manager to initialize by checking for the button
        var themeSwitcherBtn = page.Locator(".theme-switcher-btn");
        await Assertions.Expect(themeSwitcherBtn).ToBeVisibleAsync();

        // Click the theme switcher button to open the menu
        await themeSwitcherBtn.ClickAsync();

        // Wait for the menu to be visible (CSS transition)
        var themeMenu = page.Locator(".theme-menu.open");
        await Assertions.Expect(themeMenu).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5000 });
    }

    [Fact]
    public async Task ThemeSwitcher_HasMultipleOptions()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        // Wait for Blazor to be fully ready
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Click the theme switcher button to open the menu
        var themeSwitcherBtn = page.Locator(".theme-switcher-btn");
        await Assertions.Expect(themeSwitcherBtn).ToBeVisibleAsync();
        await themeSwitcherBtn.ClickAsync();

        // Wait for the menu to be visible (CSS transition)
        var themeMenu = page.Locator(".theme-menu.open");
        await Assertions.Expect(themeMenu).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5000 });

        // Count the theme options (buttons with class .theme-option)
        var options = page.Locator(".theme-option");
        var count = await options.CountAsync();

        Assert.True(count >= 2, $"Expected at least 2 theme options, got {count}");
    }

    [Fact]
    public async Task ThemeSwitcher_ChangesToDarkTheme()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        // Wait for Blazor to be fully ready
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Click the theme switcher button to open the menu
        var themeSwitcherBtn = page.Locator(".theme-switcher-btn");
        await Assertions.Expect(themeSwitcherBtn).ToBeVisibleAsync();
        await themeSwitcherBtn.ClickAsync();

        // Wait for the menu to be visible
        var themeMenu = page.Locator(".theme-menu.open");
        await Assertions.Expect(themeMenu).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5000 });

        // Click on the "dark" theme option
        var darkOption = page.Locator(".theme-option:has-text('Dark')");
        await darkOption.ClickAsync();

        // Wait for theme attribute to change
        await page.WaitForFunctionAsync(
            "() => document.documentElement.getAttribute('data-theme') === 'dark'",
            null,
            new PageWaitForFunctionOptions { Timeout = 5000 });

        // Verify theme changed to dark
        var newTheme = await page.EvaluateAsync<string>("document.documentElement.getAttribute('data-theme')");
        Assert.Equal("dark", newTheme);
    }

    [Fact]
    public async Task ThemeSwitcher_ThemePersistsOnRefresh()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        // Wait for Blazor to be fully ready
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Click the theme switcher button to open the menu
        var themeSwitcherBtn = page.Locator(".theme-switcher-btn");
        await Assertions.Expect(themeSwitcherBtn).ToBeVisibleAsync();
        await themeSwitcherBtn.ClickAsync();

        // Wait for the menu to be visible
        var themeMenu = page.Locator(".theme-menu.open");
        await Assertions.Expect(themeMenu).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5000 });

        // Click on the "dark" theme option
        var darkOption = page.Locator(".theme-option:has-text('Dark')");
        await darkOption.ClickAsync();

        // Wait for theme attribute to change
        await page.WaitForFunctionAsync(
            "() => document.documentElement.getAttribute('data-theme') === 'dark'",
            null,
            new PageWaitForFunctionOptions { Timeout = 5000 });

        // Refresh the page
        await page.ReloadAsync();
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

        // Verify theme is still dark after refresh
        var themeAfterRefresh = await page.EvaluateAsync<string>("document.documentElement.getAttribute('data-theme')");
        Assert.Equal("dark", themeAfterRefresh);
    }

    [Fact]
    public async Task ThemeSwitcher_HasAccessibilityAttributes()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        var themeSwitcherBtn = page.Locator(".theme-switcher-btn");
        await Assertions.Expect(themeSwitcherBtn).ToBeVisibleAsync();

        // Check accessibility attributes
        await Assertions.Expect(themeSwitcherBtn).ToHaveAttributeAsync("aria-label", "Change theme");
        await Assertions.Expect(themeSwitcherBtn).ToHaveAttributeAsync("aria-haspopup", "true");
    }

    [Fact]
    public async Task ThemeSwitcher_MenuClosesOnOutsideClick()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        // Wait for Blazor to be fully ready
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Click the theme switcher button to open the menu
        var themeSwitcherBtn = page.Locator(".theme-switcher-btn");
        await Assertions.Expect(themeSwitcherBtn).ToBeVisibleAsync();
        await themeSwitcherBtn.ClickAsync();

        // Wait for the menu to be visible
        var themeMenu = page.Locator(".theme-menu.open");
        await Assertions.Expect(themeMenu).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5000 });

        // Click outside the menu (on the main content area)
        await page.Locator("main, .main").First.ClickAsync();

        // Menu should close
        await Assertions.Expect(themeMenu).Not.ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5000 });
    }
}
