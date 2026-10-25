using Microsoft.Playwright;
using Xunit;

namespace MyBlog.E2E.Tests;

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
    public async Task HomePage_HasFooter()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        var footer = page.Locator("footer");
        await Assertions.Expect(footer).ToBeVisibleAsync();
    }
}
