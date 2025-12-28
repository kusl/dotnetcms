using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;
using MyBlog.Infrastructure.Repositories;
using MyBlog.Infrastructure.Services;
using Xunit;

namespace MyBlog.Tests.Integration;

public class AuthServiceTests : IAsyncDisposable
{
    private readonly BlogDbContext _context;
    private readonly AuthService _sut;
    private readonly PasswordService _passwordService;

    public AuthServiceTests()
    {
        var options = new DbContextOptionsBuilder<BlogDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _context = new BlogDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        var userRepository = new UserRepository(_context);
        _passwordService = new PasswordService();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:DefaultAdminPassword"] = "TestAdmin123!"
            })
            .Build();

        _sut = new AuthService(userRepository, _passwordService, configuration);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task AuthenticateAsync_WithValidCredentials_ReturnsUser()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            PasswordHash = _passwordService.HashPassword("TestPassword"),
            Email = "test@example.com",
            DisplayName = "Test User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var result = await _sut.AuthenticateAsync("testuser", "TestPassword");

        Assert.NotNull(result);
        Assert.Equal("testuser", result.Username);
    }

    [Fact]
    public async Task AuthenticateAsync_WithWrongPassword_ReturnsNull()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            PasswordHash = _passwordService.HashPassword("TestPassword"),
            Email = "test@example.com",
            DisplayName = "Test User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var result = await _sut.AuthenticateAsync("testuser", "WrongPassword");

        Assert.Null(result);
    }

    [Fact]
    public async Task AuthenticateAsync_WithUnknownUser_ReturnsNull()
    {
        var result = await _sut.AuthenticateAsync("nonexistent", "anypassword");
        Assert.Null(result);
    }

    [Fact]
    public async Task EnsureAdminUserAsync_WhenNoUsers_CreatesAdmin()
    {
        await _sut.EnsureAdminUserAsync();

        var admin = await _context.Users.FirstOrDefaultAsync(u => u.Username == "admin");
        Assert.NotNull(admin);
    }

    [Fact]
    public async Task EnsureAdminUserAsync_WhenUsersExist_DoesNotCreateAnother()
    {
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "existing",
            PasswordHash = "hash",
            Email = "existing@example.com",
            DisplayName = "Existing",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(existingUser);
        await _context.SaveChangesAsync();

        await _sut.EnsureAdminUserAsync();

        var userCount = await _context.Users.CountAsync();
        Assert.Equal(1, userCount);
    }
}
