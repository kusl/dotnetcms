using Microsoft.EntityFrameworkCore;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;
using MyBlog.Infrastructure.Repositories;
using Xunit;

namespace MyBlog.Tests.Integration;

public sealed class PostRepositoryUpdateTests : IAsyncDisposable
{
    private readonly BlogDbContext _context;
    private readonly PostRepository _sut;
    private readonly User _testUser;

    public PostRepositoryUpdateTests()
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

    public async ValueTask DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task UpdateAsync_WithDetachedPostAndStaleAuthor_DoesNotOverwriteAuthor()
    {
        var ct = TestContext.Current.CancellationToken;
        var post = CreateTestPost("Original", isPublished: true);
        await _sut.CreateAsync(post, ct);

        await _context.Users
            .Where(u => u.Id == _testUser.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.PasswordHash, "changed-elsewhere"), ct);

        var otherOptions = new DbContextOptionsBuilder<BlogDbContext>()
            .UseSqlite(_context.Database.GetDbConnection())
            .Options;
        await using var otherContext = new BlogDbContext(otherOptions);

        var detached = new Post
        {
            Id = post.Id,
            Title = "Updated",
            Slug = post.Slug,
            Content = post.Content,
            Summary = post.Summary,
            AuthorId = _testUser.Id,
            CreatedAtUtc = post.CreatedAtUtc,
            UpdatedAtUtc = DateTime.UtcNow,
            PublishedAtUtc = post.PublishedAtUtc,
            IsPublished = post.IsPublished,
            Author = new User
            {
                Id = _testUser.Id,
                Username = _testUser.Username,
                PasswordHash = "stale",
                Email = _testUser.Email,
                DisplayName = _testUser.DisplayName,
                CreatedAtUtc = _testUser.CreatedAtUtc
            }
        };

        await new PostRepository(otherContext).UpdateAsync(detached, ct);

        var hash = await _context.Users.AsNoTracking()
            .Where(u => u.Id == _testUser.Id)
            .Select(u => u.PasswordHash)
            .SingleAsync(ct);
        var title = await _context.Posts.AsNoTracking()
            .Where(p => p.Id == post.Id)
            .Select(p => p.Title)
            .SingleAsync(ct);

        Assert.Equal("changed-elsewhere", hash);
        Assert.Equal("Updated", title);
    }

    [Fact]
    public async Task UpdateAsync_WithTrackedPost_PersistsChanges()
    {
        var ct = TestContext.Current.CancellationToken;
        var post = CreateTestPost("Tracked", isPublished: true);
        await _sut.CreateAsync(post, ct);

        post.Title = "Tracked Updated";
        await _sut.UpdateAsync(post, ct);

        var title = await _context.Posts.AsNoTracking()
            .Where(p => p.Id == post.Id)
            .Select(p => p.Title)
            .SingleAsync(ct);
        Assert.Equal("Tracked Updated", title);
    }

    [Fact]
    public async Task GetPublishedCountAsync_CountsOnlyPublishedPosts()
    {
        var ct = TestContext.Current.CancellationToken;
        await _sut.CreateAsync(CreateTestPost("Published One", isPublished: true), ct);
        await _sut.CreateAsync(CreateTestPost("Published Two", isPublished: true), ct);
        await _sut.CreateAsync(CreateTestPost("Draft", isPublished: false), ct);

        var count = await _sut.GetPublishedCountAsync(ct);

        Assert.Equal(2, count);
    }

    private Post CreateTestPost(string title, bool isPublished) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            Slug = title.ToLowerInvariant().Replace(" ", "-"),
            Content = "Test content",
            Summary = "Test summary",
            AuthorId = _testUser.Id,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            IsPublished = isPublished,
            PublishedAtUtc = isPublished ? DateTime.UtcNow : null
        };
}
