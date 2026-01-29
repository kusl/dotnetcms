using Microsoft.Playwright;
using Xunit;

namespace MyBlog.E2E.Tests;

/// <summary>
/// E2E tests for admin pages (Epic 2: User Management, Epic 3: Content Management).
/// </summary>
[Collection(PlaywrightCollection.Name)]
public sealed class AdminPageTests(PlaywrightFixture fixture)
{
    private readonly PlaywrightFixture _fixture = fixture;

    /// <summary>
    /// Helper method to login with default admin credentials.
    /// </summary>
    private async Task LoginAsAdminAsync(IPage page)
    {
        await page.GotoAsync("/login");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await page.FillAsync("input[name='username']", "admin");
        await page.FillAsync("input[name='password']", "ChangeMe123!");
        await page.Locator("button[type='submit']").ClickAsync();

        await page.WaitForURLAsync("**/admin**", new() { Timeout = 15000 });
    }

    [Fact]
    public async Task AdminDashboard_DisplaysStatistics()
    {
        var page = await _fixture.CreatePageAsync();
        await LoginAsAdminAsync(page);

        // Dashboard should show post count or statistics
        var statCard = page.Locator(".stat-card, .dashboard-stats, text=Total Posts");
        await Assertions.Expect(statCard.First).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task AdminDashboard_HasManagePostsLink()
    {
        var page = await _fixture.CreatePageAsync();
        await LoginAsAdminAsync(page);

        var managePostsLink = page.Locator("a[href='/admin/posts']");
        await Assertions.Expect(managePostsLink).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task AdminDashboard_HasNewPostLink()
    {
        var page = await _fixture.CreatePageAsync();
        await LoginAsAdminAsync(page);

        var newPostLink = page.Locator("a[href='/admin/posts/new']");
        await Assertions.Expect(newPostLink).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task AdminDashboard_HasManageImagesLink()
    {
        var page = await _fixture.CreatePageAsync();
        await LoginAsAdminAsync(page);

        var manageImagesLink = page.Locator("a[href='/admin/images']");
        await Assertions.Expect(manageImagesLink).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task AdminPosts_LoadsSuccessfully()
    {
        var page = await _fixture.CreatePageAsync();
        await LoginAsAdminAsync(page);

        await page.ClickAsync("a[href='/admin/posts']");
        await page.WaitForURLAsync("**/admin/posts");

        // Should show posts management heading or table
        var heading = page.Locator("h1");
        await Assertions.Expect(heading).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task AdminNewPost_LoadsSuccessfully()
    {
        var page = await _fixture.CreatePageAsync();
        await LoginAsAdminAsync(page);

        await page.ClickAsync("a[href='/admin/posts/new']");
        await page.WaitForURLAsync("**/admin/posts/new");

        // Should show post editor form
        var heading = page.Locator("h1");
        await Assertions.Expect(heading).ToBeVisibleAsync(new() { Timeout = 10000 });

        // Should have title input
        var titleInput = page.Locator("input[id='title'], input[name='title'], #title");
        await Assertions.Expect(titleInput.First).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task AdminImages_LoadsSuccessfully()
    {
        var page = await _fixture.CreatePageAsync();
        await LoginAsAdminAsync(page);

        await page.ClickAsync("a[href='/admin/images']");
        await page.WaitForURLAsync("**/admin/images");

        // Should show image manager
        var heading = page.Locator("h1");
        await Assertions.Expect(heading).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task ChangePassword_LoadsSuccessfully()
    {
        var page = await _fixture.CreatePageAsync();
        await LoginAsAdminAsync(page);

        await page.GotoAsync("/admin/change-password");

        // Should show change password form
        var heading = page.Locator("h1");
        await Assertions.Expect(heading).ToContainTextAsync("Change Password");

        // Should have password fields
        var currentPasswordInput = page.Locator("input[id='currentPassword'], #currentPassword");
        var newPasswordInput = page.Locator("input[id='newPassword'], #newPassword");
        var confirmPasswordInput = page.Locator("input[id='confirmPassword'], #confirmPassword");

        await Assertions.Expect(currentPasswordInput.First).ToBeVisibleAsync(new() { Timeout = 10000 });
        await Assertions.Expect(newPasswordInput.First).ToBeVisibleAsync(new() { Timeout = 10000 });
        await Assertions.Expect(confirmPasswordInput.First).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task ChangePassword_RequiresCurrentPassword()
    {
        var page = await _fixture.CreatePageAsync();
        await LoginAsAdminAsync(page);

        await page.GotoAsync("/admin/change-password");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // The current password field should have required attribute
        var currentPasswordInput = page.Locator("#currentPassword");
        await Assertions.Expect(currentPasswordInput).ToHaveAttributeAsync("required", "");
    }

    [Fact]
    public async Task AdminNavigation_CanNavigateBetweenPages()
    {
        var page = await _fixture.CreatePageAsync();
        await LoginAsAdminAsync(page);

        // Navigate to posts
        await page.ClickAsync("a[href='/admin/posts']");
        await page.WaitForURLAsync("**/admin/posts");

        // Navigate back to dashboard via admin link
        await page.ClickAsync("nav a[href='/admin']");
        await page.WaitForURLAsync("**/admin");

        // Navigate to images
        await page.ClickAsync("a[href='/admin/images']");
        await page.WaitForURLAsync("**/admin/images");
    }

    [Fact]
    public async Task AdminDashboard_ShowsRecentPosts()
    {
        var page = await _fixture.CreatePageAsync();
        await LoginAsAdminAsync(page);

        // Dashboard should have a section for recent posts
        var recentPostsSection = page.Locator("text=Recent Posts, h2:has-text('Recent')");
        await Assertions.Expect(recentPostsSection.First).ToBeVisibleAsync(new() { Timeout = 10000 });
    }
}
