using Microsoft.Playwright;
using Xunit;

namespace MyBlog.E2E.Tests;

[Collection(PlaywrightCollection.Name)]
public sealed class AdminPageTests(PlaywrightFixture fixture)
{
    private readonly PlaywrightFixture _fixture = fixture;

    [Fact]
    public async Task AdminDashboard_DisplaysStatistics()
    {
        var page = await _fixture.CreatePageAsync();
        await page.LoginAsAdminAsync();

        var statCard = page.Locator(".stat-card").Or(page.Locator(".dashboard-stats"));
        await Assertions.Expect(statCard.First).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task AdminDashboard_HasManagePostsLink()
    {
        var page = await _fixture.CreatePageAsync();
        await page.LoginAsAdminAsync();

        var managePostsLink = page.Locator("a[href='/admin/posts']");
        await Assertions.Expect(managePostsLink).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task AdminDashboard_HasNewPostLink()
    {
        var page = await _fixture.CreatePageAsync();
        await page.LoginAsAdminAsync();

        var newPostLink = page.Locator("a[href='/admin/posts/new']");
        await Assertions.Expect(newPostLink).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task AdminDashboard_HasManageImagesLink()
    {
        var page = await _fixture.CreatePageAsync();
        await page.LoginAsAdminAsync();

        var manageImagesLink = page.Locator("a[href='/admin/images']");
        await Assertions.Expect(manageImagesLink).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task AdminPosts_LoadsSuccessfully()
    {
        var page = await _fixture.CreatePageAsync();
        await page.LoginAsAdminAsync();

        await page.GotoAsync("/admin/posts");

        var heading = page.Locator("h1");
        await Assertions.Expect(heading).ToContainTextAsync("Posts", new() { Timeout = 10000 });
    }

    [Fact]
    public async Task AdminPosts_HasNewPostButton()
    {
        var page = await _fixture.CreatePageAsync();
        await page.LoginAsAdminAsync();

        await page.GotoAsync("/admin/posts");

        var newPostLink = page.Locator("a[href='/admin/posts/new']");
        await Assertions.Expect(newPostLink).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task AdminNewPost_LoadsSuccessfully()
    {
        var page = await _fixture.CreatePageAsync();
        await page.LoginAsAdminAsync();

        await page.GotoAsync("/admin/posts/new");

        var titleInput = page.Locator("input[id='title'], input[name='title'], #title");
        await Assertions.Expect(titleInput.First).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task AdminNewPost_HasContentEditor()
    {
        var page = await _fixture.CreatePageAsync();
        await page.LoginAsAdminAsync();

        await page.GotoAsync("/admin/posts/new");

        var contentEditor = page.Locator("textarea[id='content'], textarea[name='content'], #content");
        await Assertions.Expect(contentEditor.First).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task AdminImages_LoadsSuccessfully()
    {
        var page = await _fixture.CreatePageAsync();
        await page.LoginAsAdminAsync();

        await page.GotoAsync("/admin/images");

        var heading = page.Locator("h1");
        await Assertions.Expect(heading).ToContainTextAsync("Image", new() { Timeout = 10000 });
    }

    [Fact]
    public async Task AdminNavigation_CanNavigateBetweenPages()
    {
        var page = await _fixture.CreatePageAsync();
        await page.LoginAsAdminAsync();

        var dashboardHeading = page.Locator("h1");
        await Assertions.Expect(dashboardHeading).ToContainTextAsync("Admin", new() { Timeout = 10000 });

        await page.ClickAsync("a[href='/admin/posts']");
        await page.WaitForURLAsync("**/admin/posts");

        var postsHeading = page.Locator("h1");
        await Assertions.Expect(postsHeading).ToContainTextAsync("Posts", new() { Timeout = 10000 });
    }

    [Fact]
    public async Task AdminDashboard_ShowsRecentPosts()
    {
        var page = await _fixture.CreatePageAsync();
        await page.LoginAsAdminAsync();

        var recentPostsSection = page.GetByText("Recent Posts").Or(page.Locator("h2").Filter(new() { HasText = "Recent" }));
        await Assertions.Expect(recentPostsSection.First).ToBeVisibleAsync(new() { Timeout = 10000 });
    }
}
