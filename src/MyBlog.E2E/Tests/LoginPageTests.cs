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
    public async Task LoginPage_DisplaysLoginHeading()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/login");

        var heading = page.Locator("h1");
        await Assertions.Expect(heading).ToBeVisibleAsync();
        await Assertions.Expect(heading).ToContainTextAsync("Login");
    }

    [Fact]
    public async Task LoginPage_HasUsernameField()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/login");

        var usernameInput = page.Locator("input[name='username']");
        await Assertions.Expect(usernameInput).ToBeVisibleAsync();
    }

    [Fact]
    public async Task LoginPage_HasPasswordField()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/login");

        var passwordInput = page.Locator("input[name='password']");
        await Assertions.Expect(passwordInput).ToBeVisibleAsync();
    }

    [Fact]
    public async Task LoginPage_HasSubmitButton()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/login");

        var submitButton = page.Locator("button[type='submit']");
        await Assertions.Expect(submitButton).ToBeVisibleAsync();
    }

    [Fact]
    public async Task LoginPage_FieldsAreRequired()
    {
        var page = await _fixture.CreatePageAsync();
        await page.GotoAsync("/login");

        var usernameInput = page.Locator("input[name='username']");
        var passwordInput = page.Locator("input[name='password']");

        await Assertions.Expect(usernameInput).ToHaveAttributeAsync("required", "");
        await Assertions.Expect(passwordInput).ToHaveAttributeAsync("required", "");
    }

    [Fact]
    public async Task LoginPage_PasswordFieldIsTypePassword()
    {
        var page = await _fixture.CreatePageAsync();
        await page.GotoAsync("/login");

        var passwordInput = page.Locator("input[name='password']");
        await Assertions.Expect(passwordInput).ToHaveAttributeAsync("type", "password");
    }

    [Fact]
    public async Task LoginPage_HasAntiforgeryToken()
    {
        var page = await _fixture.CreatePageAsync();
        await page.GotoAsync("/login");

        var antiforgeryInput = page.Locator("input[name='__RequestVerificationToken']");
        await Assertions.Expect(antiforgeryInput).ToBeAttachedAsync();
    }

    [Fact]
    public async Task LoginPage_FormHasCorrectAction()
    {
        var page = await _fixture.CreatePageAsync();
        await page.GotoAsync("/login");

        var form = page.Locator("form[action='/account/login']");
        await Assertions.Expect(form).ToBeVisibleAsync();
    }
}
