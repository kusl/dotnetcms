using Microsoft.EntityFrameworkCore;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;
using MyBlog.Infrastructure.Repositories;
using Xunit;

namespace MyBlog.Tests.Integration;

/// <summary>
/// Extended integration tests for the PostRepository.
/// Tests pagination, slug collision detection, and additional edge cases.
/// Uses in-memory SQLite for cross-platform compatibility.
/// </summary>
public class PostRepositoryExtendedTests : IAsyncDisposable
{
    private readonly BlogDbContext _context;
    private readonly PostRepository _sut;
    private readonly User _testUser;

    public PostRepositoryExtendedTests()
    {
        var options = new DbContextOptionsBuilder<BlogDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _context = new BlogDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _testUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            PasswordHash = "hash",
            Email = "test@example.com",
            DisplayName = "Test User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(_testUser);
        _context.SaveChanges();

        _sut = new PostRepository(_context);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task GetPublishedPostsAsync_ReturnsTotalCount()
    {
        var ct = TestContext.Current.CancellationToken;

        // Create 5 published posts
        for (var i = 0; i < 5; i++)
        {
            await _sut.CreateAsync(CreateTestPost($"Post {i}", isPublished: true), ct);
        }

        var (_, totalCount) = await _sut.GetPublishedPostsAsync(1, 2, ct);

        Assert.Equal(5, totalCount);
    }

    [Fact]
    public async Task GetPublishedPostsAsync_RespectsPageSize()
    {
        var ct = TestContext.Current.CancellationToken;

        // Create 5 published posts
        for (var i = 0; i < 5; i++)
        {
            await _sut.CreateAsync(CreateTestPost($"Post {i}", isPublished: true), ct);
        }

        var (posts, _) = await _sut.GetPublishedPostsAsync(1, 2, ct);

        Assert.Equal(2, posts.Count);
    }

    [Fact]
    public async Task GetPublishedPostsAsync_RespectsPageNumber()
    {
        var ct = TestContext.Current.CancellationToken;

        // Create 5 published posts with different dates
        for (var i = 0; i < 5; i++)
        {
            var post = CreateTestPost($"Post {i}", isPublished: true);
            post.PublishedAtUtc = DateTime.UtcNow.AddDays(-i);
            await _sut.CreateAsync(post, ct);
        }

        var (page1Posts, _) = await _sut.GetPublishedPostsAsync(1, 2, ct);
        var (page2Posts, _) = await _sut.GetPublishedPostsAsync(2, 2, ct);

        // Should be different posts on each page
        Assert.NotEqual(page1Posts[0].Id, page2Posts[0].Id);
    }

    [Fact]
    public async Task GetPublishedPostsAsync_ExcludesDrafts()
    {
        var ct = TestContext.Current.CancellationToken;

        await _sut.CreateAsync(CreateTestPost("Published", isPublished: true), ct);
        await _sut.CreateAsync(CreateTestPost("Draft", isPublished: false), ct);

        var (posts, totalCount) = await _sut.GetPublishedPostsAsync(1, 10, ct);

        Assert.Equal(1, totalCount);
        Assert.Single(posts);
        Assert.Equal("Published", posts[0].Title);
    }

    [Fact]
    public async Task GetPublishedPostsAsync_OrdersByPublishedDateDescending()
    {
        var ct = TestContext.Current.CancellationToken;

        var oldPost = CreateTestPost("Old", isPublished: true);
        oldPost.PublishedAtUtc = DateTime.UtcNow.AddDays(-5);
        await _sut.CreateAsync(oldPost, ct);

        var newPost = CreateTestPost("New", isPublished: true);
        newPost.PublishedAtUtc = DateTime.UtcNow;
        await _sut.CreateAsync(newPost, ct);

        var (posts, _) = await _sut.GetPublishedPostsAsync(1, 10, ct);

        Assert.Equal("New", posts[0].Title);
        Assert.Equal("Old", posts[1].Title);
    }

    [Fact]
    public async Task GetAllPostsAsync_IncludesBothPublishedAndDrafts()
    {
        var ct = TestContext.Current.CancellationToken;

        await _sut.CreateAsync(CreateTestPost("Published", isPublished: true), ct);
        await _sut.CreateAsync(CreateTestPost("Draft", isPublished: false), ct);

        var posts = await _sut.GetAllPostsAsync(ct);

        Assert.Equal(2, posts.Count);
    }

    [Fact]
    public async Task GetAllPostsAsync_OrdersByUpdatedDateDescending()
    {
        var ct = TestContext.Current.CancellationToken;

        var oldPost = CreateTestPost("Old", isPublished: true);
        oldPost.UpdatedAtUtc = DateTime.UtcNow.AddDays(-5);
        await _sut.CreateAsync(oldPost, ct);

        var newPost = CreateTestPost("New", isPublished: true);
        newPost.UpdatedAtUtc = DateTime.UtcNow;
        await _sut.CreateAsync(newPost, ct);

        var posts = await _sut.GetAllPostsAsync(ct);

        Assert.Equal("New", posts[0].Title);
        Assert.Equal("Old", posts[1].Title);
    }

    [Fact]
    public async Task GetCountAsync_ReturnsCorrectCount()
    {
        var ct = TestContext.Current.CancellationToken;

        await _sut.CreateAsync(CreateTestPost("Post 1"), ct);
        await _sut.CreateAsync(CreateTestPost("Post 2"), ct);
        await _sut.CreateAsync(CreateTestPost("Post 3"), ct);

        var count = await _sut.GetCountAsync(ct);

        Assert.Equal(3, count);
    }

    [Fact]
    public async Task GetCountAsync_IncludesBothPublishedAndDrafts()
    {
        var ct = TestContext.Current.CancellationToken;

        await _sut.CreateAsync(CreateTestPost("Published", isPublished: true), ct);
        await _sut.CreateAsync(CreateTestPost("Draft", isPublished: false), ct);

        var count = await _sut.GetCountAsync(ct);

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task GetRecentPostsAsync_ReturnsSpecifiedCount()
    {
        var ct = TestContext.Current.CancellationToken;

        for (var i = 0; i < 10; i++)
        {
            await _sut.CreateAsync(CreateTestPost($"Post {i}"), ct);
        }

        var posts = await _sut.GetRecentPostsAsync(5, ct);

        Assert.Equal(5, posts.Count);
    }

    [Fact]
    public async Task GetRecentPostsAsync_OrdersByUpdatedDateDescending()
    {
        var ct = TestContext.Current.CancellationToken;

        var oldPost = CreateTestPost("Old");
        oldPost.UpdatedAtUtc = DateTime.UtcNow.AddDays(-5);
        await _sut.CreateAsync(oldPost, ct);

        var newPost = CreateTestPost("New");
        newPost.UpdatedAtUtc = DateTime.UtcNow;
        await _sut.CreateAsync(newPost, ct);

        var posts = await _sut.GetRecentPostsAsync(2, ct);

        Assert.Equal("New", posts[0].Title);
    }

    [Fact]
    public async Task IsSlugTakenAsync_WithExistingSlug_ReturnsTrue()
    {
        var ct = TestContext.Current.CancellationToken;
        var post = CreateTestPost("Test", slug: "test-slug");
        await _sut.CreateAsync(post, ct);

        var isTaken = await _sut.IsSlugTakenAsync("test-slug", cancellationToken: ct);

        Assert.True(isTaken);
    }

    [Fact]
    public async Task IsSlugTakenAsync_WithNonExistentSlug_ReturnsFalse()
    {
        var ct = TestContext.Current.CancellationToken;

        var isTaken = await _sut.IsSlugTakenAsync("non-existent-slug", cancellationToken: ct);

        Assert.False(isTaken);
    }

    [Fact]
    public async Task IsSlugTakenAsync_WithExcludedPostId_ExcludesThatPost()
    {
        var ct = TestContext.Current.CancellationToken;
        var post = CreateTestPost("Test", slug: "test-slug");
        await _sut.CreateAsync(post, ct);

        // Same slug should not be considered taken when excluding the post that has it
        var isTaken = await _sut.IsSlugTakenAsync("test-slug", post.Id, ct);

        Assert.False(isTaken);
    }

    [Fact]
    public async Task IsSlugTakenAsync_WithDifferentExcludedPostId_StillReturnsTrue()
    {
        var ct = TestContext.Current.CancellationToken;
        var post = CreateTestPost("Test", slug: "test-slug");
        await _sut.CreateAsync(post, ct);

        // Different post ID should not affect the result
        var isTaken = await _sut.IsSlugTakenAsync("test-slug", Guid.NewGuid(), ct);

        Assert.True(isTaken);
    }

    [Fact]
    public async Task GetBySlugAsync_IncludesAuthorDisplayName()
    {
        var ct = TestContext.Current.CancellationToken;
        var post = CreateTestPost("Test", slug: "test-slug");
        await _sut.CreateAsync(post, ct);

        var result = await _sut.GetBySlugAsync("test-slug", ct);

        Assert.NotNull(result);
        Assert.Equal("Test User", result.AuthorDisplayName);
    }

    [Fact]
    public async Task GetByIdAsync_IncludesAuthorNavigation()
    {
        var ct = TestContext.Current.CancellationToken;
        var post = CreateTestPost("Test");
        await _sut.CreateAsync(post, ct);

        var result = await _sut.GetByIdAsync(post.Id, ct);

        Assert.NotNull(result);
        Assert.NotNull(result.Author);
        Assert.Equal("testuser", result.Author.Username);
    }

    [Fact]
    public async Task UpdateAsync_ChangesModifiedFields()
    {
        var ct = TestContext.Current.CancellationToken;
        var post = CreateTestPost("Original Title");
        await _sut.CreateAsync(post, ct);

        post.Title = "Updated Title";
        post.Content = "Updated Content";
        post.UpdatedAtUtc = DateTime.UtcNow;
        await _sut.UpdateAsync(post, ct);

        var updated = await _sut.GetByIdAsync(post.Id, ct);
        Assert.NotNull(updated);
        Assert.Equal("Updated Title", updated.Title);
        Assert.Equal("Updated Content", updated.Content);
    }

    [Fact]
    public async Task DeleteAsync_RemovesPost()
    {
        var ct = TestContext.Current.CancellationToken;
        var post = CreateTestPost("To Delete");
        await _sut.CreateAsync(post, ct);

        await _sut.DeleteAsync(post.Id, ct);

        var deleted = await _sut.GetByIdAsync(post.Id, ct);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentId_DoesNotThrow()
    {
        var ct = TestContext.Current.CancellationToken;

        // Should not throw
        await _sut.DeleteAsync(Guid.NewGuid(), ct);
    }

    private Post CreateTestPost(string title, string? slug = null, bool isPublished = true)
    {
        return new Post
        {
            Id = Guid.NewGuid(),
            Title = title,
            Slug = slug ?? title.ToLower().Replace(" ", "-"),
            Content = "Test content",
            Summary = "Test summary",
            AuthorId = _testUser.Id,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            IsPublished = isPublished,
            PublishedAtUtc = isPublished ? DateTime.UtcNow : null
        };
    }
}
