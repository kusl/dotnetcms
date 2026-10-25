using Microsoft.Playwright;
using Xunit;

namespace MyBlog.E2E.Tests;

[Collection(PlaywrightCollection.Name)]
public sealed class ThemeSwitcherTests(PlaywrightFixture fixture)
{
    private readonly PlaywrightFixture _fixture = fixture;

    [Fact]
    public async Task ThemeSwitcher_IsVisibleOnPage()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        var themeSwitcher = page.Locator(".theme-switcher");
        await Assertions.Expect(themeSwitcher).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ThemeSwitcher_ButtonIsClickable()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

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

        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var themeSwitcherBtn = page.Locator(".theme-switcher-btn");
        await Assertions.Expect(themeSwitcherBtn).ToBeVisibleAsync();

        await themeSwitcherBtn.ClickAsync();

        var themeMenu = page.Locator(".theme-menu.open");
        await Assertions.Expect(themeMenu).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5000 });
    }

    [Fact]
    public async Task ThemeSwitcher_HasMultipleOptions()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var themeSwitcherBtn = page.Locator(".theme-switcher-btn");
        await Assertions.Expect(themeSwitcherBtn).ToBeVisibleAsync();
        await themeSwitcherBtn.ClickAsync();

        var themeMenu = page.Locator(".theme-menu.open");
        await Assertions.Expect(themeMenu).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5000 });

        var options = page.Locator(".theme-option");
        var count = await options.CountAsync();

        Assert.True(count >= 2, $"Expected at least 2 theme options, got {count}");
    }

    [Fact]
    public async Task ThemeSwitcher_ChangesToDarkTheme()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var themeSwitcherBtn = page.Locator(".theme-switcher-btn");
        await Assertions.Expect(themeSwitcherBtn).ToBeVisibleAsync();
        await themeSwitcherBtn.ClickAsync();

        var themeMenu = page.Locator(".theme-menu.open");
        await Assertions.Expect(themeMenu).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5000 });

        var darkOption = page.Locator(".theme-option:has-text('Dark')");
        await darkOption.ClickAsync();

        await page.WaitForFunctionAsync(
            "() => document.documentElement.getAttribute('data-theme') === 'dark'",
            null,
            new PageWaitForFunctionOptions { Timeout = 5000 });

        var newTheme = await page.EvaluateAsync<string>("document.documentElement.getAttribute('data-theme')");
        Assert.Equal("dark", newTheme);
    }

    [Fact]
    public async Task ThemeSwitcher_ThemePersistsOnRefresh()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var themeSwitcherBtn = page.Locator(".theme-switcher-btn");
        await Assertions.Expect(themeSwitcherBtn).ToBeVisibleAsync();
        await themeSwitcherBtn.ClickAsync();

        var themeMenu = page.Locator(".theme-menu.open");
        await Assertions.Expect(themeMenu).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5000 });

        var darkOption = page.Locator(".theme-option:has-text('Dark')");
        await darkOption.ClickAsync();

        await page.WaitForFunctionAsync(
            "() => document.documentElement.getAttribute('data-theme') === 'dark'",
            null,
            new PageWaitForFunctionOptions { Timeout = 5000 });

        await page.ReloadAsync();
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

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

        await Assertions.Expect(themeSwitcherBtn).ToHaveAttributeAsync("aria-label", "Change theme");
        await Assertions.Expect(themeSwitcherBtn).ToHaveAttributeAsync("aria-haspopup", "true");
    }

    [Fact]
    public async Task ThemeSwitcher_MenuClosesOnOutsideClick()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var themeSwitcherBtn = page.Locator(".theme-switcher-btn");
        await Assertions.Expect(themeSwitcherBtn).ToBeVisibleAsync();
        await themeSwitcherBtn.ClickAsync();

        var themeMenu = page.Locator(".theme-menu.open");
        await Assertions.Expect(themeMenu).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5000 });

        await page.Locator("main, .main").First.ClickAsync();

        await Assertions.Expect(themeMenu).Not.ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5000 });
    }
}
