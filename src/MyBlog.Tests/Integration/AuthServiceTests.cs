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
    public async Task ValidateCredentialsAsync_WithValidCredentials_ReturnsUser()
    {
        var ct = TestContext.Current.CancellationToken;
        var password = "TestPassword123";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            PasswordHash = _passwordService.HashPassword(password),
            Email = "test@example.com",
            DisplayName = "Test User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        var result = await _sut.ValidateCredentialsAsync("testuser", password, ct);

        Assert.NotNull(result);
        Assert.Equal("testuser", result.Username);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_WithInvalidPassword_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            PasswordHash = _passwordService.HashPassword("CorrectPassword"),
            Email = "test@example.com",
            DisplayName = "Test User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        var result = await _sut.ValidateCredentialsAsync("testuser", "WrongPassword", ct);

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_WithNonExistentUser_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var result = await _sut.ValidateCredentialsAsync("nonexistent", "password", ct);
        Assert.Null(result);
    }

    [Fact]
    public async Task EnsureAdminUserAsync_WhenNoUsersExist_CreatesAdmin()
    {
        var ct = TestContext.Current.CancellationToken;
        await _sut.EnsureAdminUserAsync(ct);

        var admin = await _context.Users.FirstOrDefaultAsync(u => u.Username == "admin", ct);
        Assert.NotNull(admin);
    }

    [Fact]
    public async Task EnsureAdminUserAsync_WhenUsersExist_DoesNotCreateAnother()
    {
        var ct = TestContext.Current.CancellationToken;
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
        await _context.SaveChangesAsync(ct);

        await _sut.EnsureAdminUserAsync(ct);

        var userCount = await _context.Users.CountAsync(ct);
        Assert.Equal(1, userCount);
    }
}
