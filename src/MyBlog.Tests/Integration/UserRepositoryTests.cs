using Microsoft.EntityFrameworkCore;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;
using MyBlog.Infrastructure.Repositories;
using Xunit;

namespace MyBlog.Tests.Integration;

/// <summary>
/// Integration tests for the UserRepository.
/// Uses in-memory SQLite for cross-platform compatibility.
/// </summary>
public class UserRepositoryTests : IAsyncDisposable
{
    private readonly BlogDbContext _context;
    private readonly UserRepository _sut;

    public UserRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<BlogDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _context = new BlogDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _sut = new UserRepository(_context);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task CreateAsync_AddsUserToDatabase()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = CreateTestUser("newuser");

        await _sut.CreateAsync(user, ct);

        var saved = await _context.Users.FindAsync([user.Id], ct);
        Assert.NotNull(saved);
        Assert.Equal("newuser", saved.Username);
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ReturnsUser()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = CreateTestUser("testuser");
        await _sut.CreateAsync(user, ct);

        var result = await _sut.GetByIdAsync(user.Id, ct);

        Assert.NotNull(result);
        Assert.Equal("testuser", result.Username);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistingId_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;

        var result = await _sut.GetByIdAsync(Guid.NewGuid(), ct);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByUsernameAsync_WithExistingUsername_ReturnsUser()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = CreateTestUser("findme");
        await _sut.CreateAsync(user, ct);

        var result = await _sut.GetByUsernameAsync("findme", ct);

        Assert.NotNull(result);
        Assert.Equal("findme", result.Username);
    }

    [Fact]
    public async Task GetByUsernameAsync_IsCaseInsensitive()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = CreateTestUser("CaseSensitive");
        await _sut.CreateAsync(user, ct);

        var result = await _sut.GetByUsernameAsync("casesensitive", ct);

        Assert.NotNull(result);
        Assert.Equal("CaseSensitive", result.Username);
    }

    [Fact]
    public async Task GetByUsernameAsync_WithNonExistingUsername_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;

        var result = await _sut.GetByUsernameAsync("nonexistent", ct);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllUsersOrderedByUsername()
    {
        var ct = TestContext.Current.CancellationToken;
        await _sut.CreateAsync(CreateTestUser("charlie"), ct);
        await _sut.CreateAsync(CreateTestUser("alice"), ct);
        await _sut.CreateAsync(CreateTestUser("bob"), ct);

        var result = await _sut.GetAllAsync(ct);

        Assert.Equal(3, result.Count);
        Assert.Equal("alice", result[0].Username);
        Assert.Equal("bob", result[1].Username);
        Assert.Equal("charlie", result[2].Username);
    }

    [Fact]
    public async Task GetAllAsync_WithNoUsers_ReturnsEmptyList()
    {
        var ct = TestContext.Current.CancellationToken;

        var result = await _sut.GetAllAsync(ct);

        Assert.Empty(result);
    }

    [Fact]
    public async Task AnyUsersExistAsync_WithNoUsers_ReturnsFalse()
    {
        var ct = TestContext.Current.CancellationToken;

        var result = await _sut.AnyUsersExistAsync(ct);

        Assert.False(result);
    }

    [Fact]
    public async Task AnyUsersExistAsync_WithUsers_ReturnsTrue()
    {
        var ct = TestContext.Current.CancellationToken;
        await _sut.CreateAsync(CreateTestUser("anyuser"), ct);

        var result = await _sut.AnyUsersExistAsync(ct);

        Assert.True(result);
    }

    [Fact]
    public async Task UpdateAsync_ModifiesExistingUser()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = CreateTestUser("updateme");
        await _sut.CreateAsync(user, ct);

        user.DisplayName = "Updated Display Name";
        user.Email = "updated@example.com";
        await _sut.UpdateAsync(user, ct);

        var updated = await _sut.GetByIdAsync(user.Id, ct);
        Assert.NotNull(updated);
        Assert.Equal("Updated Display Name", updated.DisplayName);
        Assert.Equal("updated@example.com", updated.Email);
    }

    [Fact]
    public async Task UpdateAsync_CanChangePasswordHash()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = CreateTestUser("passwordchange");
        await _sut.CreateAsync(user, ct);

        user.PasswordHash = "newhash";
        await _sut.UpdateAsync(user, ct);

        var updated = await _sut.GetByIdAsync(user.Id, ct);
        Assert.NotNull(updated);
        Assert.Equal("newhash", updated.PasswordHash);
    }

    [Fact]
    public async Task DeleteAsync_RemovesUserFromDatabase()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = CreateTestUser("deleteme");
        await _sut.CreateAsync(user, ct);

        await _sut.DeleteAsync(user.Id, ct);

        var deleted = await _context.Users.FindAsync([user.Id], ct);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentId_DoesNotThrow()
    {
        var ct = TestContext.Current.CancellationToken;

        // Should not throw
        await _sut.DeleteAsync(Guid.NewGuid(), ct);
    }

    [Fact]
    public async Task DeleteAsync_DecreasesUserCount()
    {
        var ct = TestContext.Current.CancellationToken;
        var user1 = CreateTestUser("user1");
        var user2 = CreateTestUser("user2");
        await _sut.CreateAsync(user1, ct);
        await _sut.CreateAsync(user2, ct);

        await _sut.DeleteAsync(user1.Id, ct);

        var allUsers = await _sut.GetAllAsync(ct);
        Assert.Single(allUsers);
        Assert.Equal("user2", allUsers[0].Username);
    }

    [Fact]
    public async Task CreateAsync_PreservesAllFields()
    {
        var ct = TestContext.Current.CancellationToken;
        var createdAt = DateTime.UtcNow.AddDays(-1);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "fulluser",
            PasswordHash = "hash123",
            Email = "full@example.com",
            DisplayName = "Full User",
            CreatedAtUtc = createdAt
        };

        await _sut.CreateAsync(user, ct);

        var saved = await _sut.GetByIdAsync(user.Id, ct);
        Assert.NotNull(saved);
        Assert.Equal("fulluser", saved.Username);
        Assert.Equal("hash123", saved.PasswordHash);
        Assert.Equal("full@example.com", saved.Email);
        Assert.Equal("Full User", saved.DisplayName);
        Assert.Equal(createdAt, saved.CreatedAtUtc);
    }

    private static User CreateTestUser(string username)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            PasswordHash = "hash",
            Email = $"{username}@example.com",
            DisplayName = $"Display {username}",
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
