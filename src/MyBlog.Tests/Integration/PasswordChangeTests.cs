using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;
using MyBlog.Infrastructure.Repositories;
using MyBlog.Infrastructure.Services;
using Xunit;

namespace MyBlog.Tests.Integration;

public class PasswordChangeTests : IAsyncDisposable
{
    private readonly BlogDbContext _context;
    private readonly AuthService _sut;
    private readonly PasswordService _passwordService = new(new PasswordHasher<User>());

    public PasswordChangeTests()
    {
        var options = new DbContextOptionsBuilder<BlogDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _context = new BlogDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        var userRepository = new UserRepository(_context);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:DefaultAdminPassword"] = "TestAdmin123!"
            })
            .Build();

        _sut = new AuthService(userRepository, _passwordService, configuration);
    }

    public async ValueTask DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task ChangePasswordAsync_WithCorrectCurrentPassword_ReturnsTrue()
    {
        var ct = TestContext.Current.CancellationToken;
        var originalPassword = "OriginalPass123!";
        var newPassword = "NewPassword456!";

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            PasswordHash = _passwordService.HashPassword(originalPassword),
            Email = "test@example.com",
            DisplayName = "Test User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        var result = await _sut.ChangePasswordAsync(user.Id, originalPassword, newPassword, ct);

        Assert.True(result);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithCorrectPassword_AllowsLoginWithNewPassword()
    {
        var ct = TestContext.Current.CancellationToken;
        var originalPassword = "OriginalPass123!";
        var newPassword = "NewPassword456!";

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            PasswordHash = _passwordService.HashPassword(originalPassword),
            Email = "test@example.com",
            DisplayName = "Test User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        await _sut.ChangePasswordAsync(user.Id, originalPassword, newPassword, ct);

        var authenticated = await _sut.AuthenticateAsync("testuser", newPassword, ct);
        Assert.NotNull(authenticated);

        var oldAuth = await _sut.AuthenticateAsync("testuser", originalPassword, ct);
        Assert.Null(oldAuth);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithWrongCurrentPassword_ReturnsFalse()
    {
        var ct = TestContext.Current.CancellationToken;
        var correctPassword = "CorrectPass123!";
        var wrongPassword = "WrongPassword!";

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            PasswordHash = _passwordService.HashPassword(correctPassword),
            Email = "test@example.com",
            DisplayName = "Test User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        var result = await _sut.ChangePasswordAsync(user.Id, wrongPassword, "NewPass123!", ct);

        Assert.False(result);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithWrongPassword_DoesNotChangePassword()
    {
        var ct = TestContext.Current.CancellationToken;
        var correctPassword = "CorrectPass123!";

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            PasswordHash = _passwordService.HashPassword(correctPassword),
            Email = "test@example.com",
            DisplayName = "Test User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        await _sut.ChangePasswordAsync(user.Id, "WrongPassword!", "NewPass123!", ct);

        var authenticated = await _sut.AuthenticateAsync("testuser", correctPassword, ct);
        Assert.NotNull(authenticated);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithNonExistentUser_ReturnsFalse()
    {
        var ct = TestContext.Current.CancellationToken;
        var result = await _sut.ChangePasswordAsync(Guid.NewGuid(), "any", "password", ct);
        Assert.False(result);
    }
}
