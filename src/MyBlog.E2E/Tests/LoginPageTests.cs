using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace MyBlog.E2E.Tests;

[Collection(PlaywrightCollection.Name)]
public sealed class LoginPageTests(PlaywrightFixture fixture)
{
    private readonly PlaywrightFixture _fixture = fixture;

    [Fact]
    public async Task LoginPage_LoadsSuccessfully()
    {
        var page = await _fixture.CreatePageAsync();
        var response = await page.GotoAsync("/login");

        Assert.NotNull(response);
        Assert.True(response.Ok, $"Expected OK response, got {response.Status}");
    }

    [Fact]
    public async Task LoginPage_DisplaysLoginForm()
    {
        var page = await _fixture.CreatePageAsync();
        await page.GotoAsync("/login");

        // Use Web Assertions to wait for visibility
        await Assertions.Expect(page.Locator("input[name='username']")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("input[name='password']")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("button[type='submit']")).ToBeVisibleAsync();
    }
}
