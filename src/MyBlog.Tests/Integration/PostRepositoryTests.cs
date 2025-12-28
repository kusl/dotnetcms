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
        var post = CreateTestPost("Test Post");

        var result = await _sut.CreateAsync(post);

        Assert.NotEqual(Guid.Empty, result.Id);
        var saved = await _context.Posts.FindAsync(result.Id);
        Assert.NotNull(saved);
        Assert.Equal("Test Post", saved.Title);
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ReturnsPost()
    {
        var post = CreateTestPost("Test Post");
        await _sut.CreateAsync(post);

        var result = await _sut.GetByIdAsync(post.Id);

        Assert.NotNull(result);
        Assert.Equal("Test Post", result.Title);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistingId_ReturnsNull()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task GetBySlugAsync_WithExistingSlug_ReturnsPost()
    {
        var post = CreateTestPost("Test Post", "test-post");
        await _sut.CreateAsync(post);

        var result = await _sut.GetBySlugAsync("test-post");

        Assert.NotNull(result);
        Assert.Equal("Test Post", result.Title);
    }

    [Fact]
    public async Task GetPublishedPostsAsync_ReturnsOnlyPublishedPosts()
    {
        var published = CreateTestPost("Published", "published", true);
        var draft = CreateTestPost("Draft", "draft", false);
        await _sut.CreateAsync(published);
        await _sut.CreateAsync(draft);

        var (posts, count) = await _sut.GetPublishedPostsAsync(1, 10);

        Assert.Single(posts);
        Assert.Equal("Published", posts[0].Title);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task UpdateAsync_ModifiesExistingPost()
    {
        var post = CreateTestPost("Original Title");
        await _sut.CreateAsync(post);

        post.Title = "Updated Title";
        await _sut.UpdateAsync(post);

        var updated = await _context.Posts.FindAsync(post.Id);
        Assert.Equal("Updated Title", updated!.Title);
    }

    [Fact]
    public async Task DeleteAsync_RemovesPostFromDatabase()
    {
        var post = CreateTestPost("To Delete");
        await _sut.CreateAsync(post);

        await _sut.DeleteAsync(post.Id);

        var deleted = await _context.Posts.FindAsync(post.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task GetCountAsync_ReturnsCorrectCount()
    {
        await _sut.CreateAsync(CreateTestPost("Post 1"));
        await _sut.CreateAsync(CreateTestPost("Post 2"));
        await _sut.CreateAsync(CreateTestPost("Post 3"));

        var count = await _sut.GetCountAsync();

        Assert.Equal(3, count);
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
