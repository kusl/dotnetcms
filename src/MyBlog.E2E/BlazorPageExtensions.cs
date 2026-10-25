using Microsoft.Playwright;

namespace MyBlog.E2E;

public static class BlazorPageExtensions
{
    public const string AdminUsername = "admin";
    public const string AdminPassword = "ChangeMe123!";

    private const string InteractiveMarkerSelector = ".layout[data-blazor-interactive='true']";

    private const float DefaultTimeoutMs = 30000;

    public static async Task WaitForBlazorInteractiveAsync(this IPage page, float timeout = DefaultTimeoutMs)
    {
        var marker = page.Locator(InteractiveMarkerSelector);
        await Assertions.Expect(marker).ToBeAttachedAsync(new() { Timeout = timeout });
    }

    public static async Task SubmitLoginAsync(this IPage page, string username, string password, string expectedUrl)
    {
        await page.GotoAsync("/login");
        await page.WaitForBlazorInteractiveAsync();

        await page.FillAsync("input[name='username']", username);
        await page.FillAsync("input[name='password']", password);

        await Task.WhenAll(
            page.WaitForURLAsync(expectedUrl, new() { Timeout = DefaultTimeoutMs }),
            page.Locator("button[type='submit']").ClickAsync());

        await page.WaitForBlazorInteractiveAsync();
    }

    public static Task LoginAsAdminAsync(this IPage page) =>
        page.SubmitLoginAsync(AdminUsername, AdminPassword, "**/admin**");
}
