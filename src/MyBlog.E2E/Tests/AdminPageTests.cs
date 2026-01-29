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

        // Click and wait for navigation in parallel to handle form submission properly
        await Task.WhenAll(
            page.WaitForURLAsync("**/admin**", new() { Timeout = 30000 }),
            page.Locator("button[type='submit']").ClickAsync()
        );
    }

    [Fact]
    public async Task AdminDashboard_DisplaysStatistics()
    {
        var page = await _fixture.CreatePageAsync();
        await LoginAsAdminAsync(page);

        // Dashboard should show post count or statistics - use proper selector pattern
        // Cannot mix CSS selectors with text= in a comma-separated list
        var statCard = page.Locator(".stat-card").Or(page.Locator(".dashboard-stats"));
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

        await page.GotoAsync("/admin/posts");

        var heading = page.Locator("h1");
        await Assertions.Expect(heading).ToContainTextAsync("Posts", new() { Timeout = 10000 });
    }

    [Fact]
    public async Task AdminPosts_HasNewPostButton()
    {
        var page = await _fixture.CreatePageAsync();
        await LoginAsAdminAsync(page);

        await page.GotoAsync("/admin/posts");

        var newPostLink = page.Locator("a[href='/admin/posts/new']");
        await Assertions.Expect(newPostLink).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task AdminNewPost_LoadsSuccessfully()
    {
        var page = await _fixture.CreatePageAsync();
        await LoginAsAdminAsync(page);

        await page.GotoAsync("/admin/posts/new");

        // Should have title input
        var titleInput = page.Locator("input[id='title'], input[name='title'], #title");
        await Assertions.Expect(titleInput.First).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task AdminNewPost_HasContentEditor()
    {
        var page = await _fixture.CreatePageAsync();
        await LoginAsAdminAsync(page);

        await page.GotoAsync("/admin/posts/new");

        // Should have content textarea
        var contentEditor = page.Locator("textarea[id='content'], textarea[name='content'], #content");
        await Assertions.Expect(contentEditor.First).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task AdminImages_LoadsSuccessfully()
    {
        var page = await _fixture.CreatePageAsync();
        await LoginAsAdminAsync(page);

        await page.GotoAsync("/admin/images");

        var heading = page.Locator("h1");
        await Assertions.Expect(heading).ToContainTextAsync("Image", new() { Timeout = 10000 });
    }

    [Fact]
    public async Task AdminNavigation_CanNavigateBetweenPages()
    {
        var page = await _fixture.CreatePageAsync();
        await LoginAsAdminAsync(page);

        // Start at dashboard
        var dashboardHeading = page.Locator("h1");
        await Assertions.Expect(dashboardHeading).ToContainTextAsync("Admin", new() { Timeout = 10000 });

        // Navigate to posts
        await page.ClickAsync("a[href='/admin/posts']");
        await page.WaitForURLAsync("**/admin/posts");

        var postsHeading = page.Locator("h1");
        await Assertions.Expect(postsHeading).ToContainTextAsync("Posts", new() { Timeout = 10000 });
    }

    [Fact]
    public async Task AdminDashboard_ShowsRecentPosts()
    {
        var page = await _fixture.CreatePageAsync();
        await LoginAsAdminAsync(page);

        // Look for recent posts section - use Or pattern instead of comma-separated selectors
        var recentPostsSection = page.GetByText("Recent Posts").Or(page.Locator("h2").Filter(new() { HasText = "Recent" }));
        await Assertions.Expect(recentPostsSection.First).ToBeVisibleAsync(new() { Timeout = 10000 });
    }
}
