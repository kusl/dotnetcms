using Microsoft.EntityFrameworkCore;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;
using MyBlog.Infrastructure.Repositories;
using Xunit;

namespace MyBlog.Tests.Integration;

public class PostRepositoryTests : IAsyncDisposable
{
    private readonly BlogDbContext _context;
    private readonly PostRepository _sut;
    private readonly User _testUser;

    public PostRepositoryTests()
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
    public async Task CreateAsync_AddsPostToDatabase()
    {
        var ct = TestContext.Current.CancellationToken;
        var post = CreateTestPost("Test Post");

        var result = await _sut.CreateAsync(post, ct);

        Assert.NotEqual(Guid.Empty, result.Id);
        var saved = await _context.Posts.FindAsync([result.Id], ct);
        Assert.NotNull(saved);
        Assert.Equal("Test Post", saved.Title);
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ReturnsPost()
    {
        var ct = TestContext.Current.CancellationToken;
        var post = CreateTestPost("Test Post");
        await _sut.CreateAsync(post, ct);

        var result = await _sut.GetByIdAsync(post.Id, ct);

        Assert.NotNull(result);
        Assert.Equal("Test Post", result.Title);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistingId_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var result = await _sut.GetByIdAsync(Guid.NewGuid(), ct);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetBySlugAsync_WithExistingSlug_ReturnsPost()
    {
        var ct = TestContext.Current.CancellationToken;
        var post = CreateTestPost("Test Post", "test-post");
        await _sut.CreateAsync(post, ct);

        var result = await _sut.GetBySlugAsync("test-post", ct);

        Assert.NotNull(result);
        Assert.Equal("Test Post", result.Title);
    }

    [Fact]
    public async Task GetPublishedPostsAsync_ReturnsOnlyPublishedPosts()
    {
        var ct = TestContext.Current.CancellationToken;
        await _sut.CreateAsync(CreateTestPost("Published", isPublished: true), ct);
        await _sut.CreateAsync(CreateTestPost("Draft", isPublished: false), ct);

        var (posts, totalCount) = await _sut.GetPublishedPostsAsync(1, 10, ct);

        Assert.Single(posts);
        Assert.Equal("Published", posts.First().Title);
        Assert.Equal(1, totalCount);
    }

    [Fact]
    public async Task UpdateAsync_ModifiesPost()
    {
        var ct = TestContext.Current.CancellationToken;
        var post = CreateTestPost("Original Title");
        await _sut.CreateAsync(post, ct);

        post.Title = "Updated Title";
        await _sut.UpdateAsync(post, ct);

        var result = await _sut.GetByIdAsync(post.Id, ct);
        Assert.Equal("Updated Title", result!.Title);
    }

    [Fact]
    public async Task DeleteAsync_RemovesPost()
    {
        var ct = TestContext.Current.CancellationToken;
        var post = CreateTestPost("To Delete");
        await _sut.CreateAsync(post, ct);

        await _sut.DeleteAsync(post.Id, ct);

        var result = await _sut.GetByIdAsync(post.Id, ct);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetPublishedPostsAsync_ReturnsCorrectCount()
    {
        var ct = TestContext.Current.CancellationToken;
        await _sut.CreateAsync(CreateTestPost("Post 1", isPublished: true), ct);
        await _sut.CreateAsync(CreateTestPost("Post 2", isPublished: true), ct);
        await _sut.CreateAsync(CreateTestPost("Draft", isPublished: false), ct);

        var (_, totalCount) = await _sut.GetPublishedPostsAsync(1, 10, ct);

        Assert.Equal(2, totalCount);
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