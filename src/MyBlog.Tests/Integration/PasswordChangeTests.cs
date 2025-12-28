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
    private readonly PasswordService _passwordService = new();
    private readonly UserRepository _userRepository;

    public PasswordChangeTests()
    {
        var options = new DbContextOptionsBuilder<BlogDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _context = new BlogDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _userRepository = new UserRepository(_context);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:DefaultAdminPassword"] = "TestAdmin123!"
            })
            .Build();

        _sut = new AuthService(_userRepository, _passwordService, configuration);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

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

        // Should authenticate with new password
        var authenticated = await _sut.AuthenticateAsync("testuser", newPassword, ct);
        Assert.NotNull(authenticated);

        // Should NOT authenticate with old password
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

        // Original password should still work
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

    [Fact]
    public async Task ResetPasswordAsync_SetsNewPassword()
    {
        var ct = TestContext.Current.CancellationToken;
        var originalPassword = "OriginalPass123!";
        var newPassword = "ResetPassword789!";

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

        await _sut.ResetPasswordAsync(user.Id, newPassword, ct);

        var authenticated = await _sut.AuthenticateAsync("testuser", newPassword, ct);
        Assert.NotNull(authenticated);
    }

    [Fact]
    public async Task ResetPasswordAsync_WithNonExistentUser_ThrowsException()
    {
        var ct = TestContext.Current.CancellationToken;
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ResetPasswordAsync(Guid.NewGuid(), "password", ct));
    }
}
