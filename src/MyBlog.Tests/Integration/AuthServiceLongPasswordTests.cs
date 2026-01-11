using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;
using MyBlog.Infrastructure.Repositories;
using MyBlog.Infrastructure.Services;
using Xunit;

namespace MyBlog.Tests.Integration;

/// <summary>
/// Tests for long password support and account lockout behavior.
/// Uses in-memory SQLite for cross-platform compatibility (Windows/Linux/macOS).
/// </summary>
public sealed class AuthServiceLongPasswordTests : IAsyncDisposable
{
    private readonly BlogDbContext _context;
    private readonly AuthService _sut;
    private readonly PasswordService _passwordService;

    public AuthServiceLongPasswordTests()
    {
        // Use in-memory SQLite - works on all platforms without file locking issues
        var options = new DbContextOptionsBuilder<BlogDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _context = new BlogDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _passwordService = new PasswordService();
        var userRepository = new UserRepository(_context);
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

    // =========================================================================
    // Long Password Tests (128+ characters)
    // =========================================================================

    [Fact]
    public async Task AuthenticateAsync_With128CharacterPassword_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var longPassword = new string('a', 128);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "long-pass-user",
            PasswordHash = _passwordService.HashPassword(longPassword),
            Email = "longpass@example.com",
            DisplayName = "Long Password User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        var result = await _sut.AuthenticateAsync("long-pass-user", longPassword, ct);

        Assert.NotNull(result);
        Assert.Equal("long-pass-user", result.Username);
    }

    [Fact]
    public async Task AuthenticateAsync_With256CharacterPassword_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var longPassword = new string('x', 256);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "very-long-pass-user",
            PasswordHash = _passwordService.HashPassword(longPassword),
            Email = "very-long@example.com",
            DisplayName = "Very Long Password User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        var result = await _sut.AuthenticateAsync("very-long-pass-user", longPassword, ct);

        Assert.NotNull(result);
        Assert.Equal("very-long-pass-user", result.Username);
    }

    [Fact]
    public async Task AuthenticateAsync_With512CharacterPassword_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var longPassword = new string('P', 512);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "extralong-pass-user",
            PasswordHash = _passwordService.HashPassword(longPassword),
            Email = "extralong@example.com",
            DisplayName = "Extra Long Password User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        var result = await _sut.AuthenticateAsync("extralong-pass-user", longPassword, ct);

        Assert.NotNull(result);
        Assert.Equal("extralong-pass-user", result.Username);
    }

    [Fact]
    public async Task ChangePasswordAsync_With128CharacterNewPassword_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var originalPassword = "ShortPass123!";
        var newLongPassword = new string('N', 128);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "change-pass-long-user",
            PasswordHash = _passwordService.HashPassword(originalPassword),
            Email = "changelong@example.com",
            DisplayName = "Change Long Password User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        var result = await _sut.ChangePasswordAsync(user.Id, originalPassword, newLongPassword, ct);

        Assert.True(result);

        // Verify new password works
        var authenticated = await _sut.AuthenticateAsync("change-pass-long-user", newLongPassword, ct);
        Assert.NotNull(authenticated);
    }

    [Fact]
    public async Task AuthenticateAsync_WithComplexLongPassword_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        // Mix of characters, 200 chars long
        var complexPassword = string.Concat(
            new string('A', 50),
            new string('1', 50),
            new string('!', 50),
            new string('z', 50)
        );

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "complex-pass-user",
            PasswordHash = _passwordService.HashPassword(complexPassword),
            Email = "complex@example.com",
            DisplayName = "Complex Password User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        var result = await _sut.AuthenticateAsync("complex-pass-user", complexPassword, ct);

        Assert.NotNull(result);
    }

    // =========================================================================
    // No Account Lockout Tests
    // =========================================================================

    [Fact]
    public async Task AuthenticateAsync_After100FailedAttempts_StillAllowsLogin()
    {
        var ct = TestContext.Current.CancellationToken;
        var correctPassword = "CorrectPassword123!";

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "no-lockout-user",
            PasswordHash = _passwordService.HashPassword(correctPassword),
            Email = "no-lockout@example.com",
            DisplayName = "No Lockout User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        // Attempt 100 failed logins
        for (var i = 0; i < 100; i++)
        {
            var failedResult = await _sut.AuthenticateAsync("no-lockout-user", "WrongPassword", ct);
            Assert.Null(failedResult);
        }

        // User should still be able to log in with correct password
        var successResult = await _sut.AuthenticateAsync("no-lockout-user", correctPassword, ct);
        Assert.NotNull(successResult);
        Assert.Equal("no-lockout-user", successResult.Username);
    }

    [Fact]
    public async Task AuthenticateAsync_After1000FailedAttempts_StillAllowsLogin()
    {
        var ct = TestContext.Current.CancellationToken;
        var correctPassword = "CorrectPassword123!";

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "no-lockout1000user",
            PasswordHash = _passwordService.HashPassword(correctPassword),
            Email = "no-lockout1000@example.com",
            DisplayName = "No Lockout 1000 User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        // Attempt 1000 failed logins
        for (var i = 0; i < 1000; i++)
        {
            var failedResult = await _sut.AuthenticateAsync("no-lockout1000user", $"WrongPassword{i}", ct);
            Assert.Null(failedResult);
        }

        // User should still be able to log in with correct password
        var successResult = await _sut.AuthenticateAsync("no-lockout1000user", correctPassword, ct);
        Assert.NotNull(successResult);
        Assert.Equal("no-lockout1000user", successResult.Username);
    }

    [Fact]
    public async Task ChangePasswordAsync_AfterManyFailedAttempts_StillWorks()
    {
        var ct = TestContext.Current.CancellationToken;
        var originalPassword = "OriginalPass123!";
        var newPassword = "NewPassword456!";

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "change-pass-no-lockout",
            PasswordHash = _passwordService.HashPassword(originalPassword),
            Email = "change-pass-no-lockout@example.com",
            DisplayName = "Change Password No Lockout",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        // Attempt 50 failed password changes
        for (var i = 0; i < 50; i++)
        {
            var failedResult = await _sut.ChangePasswordAsync(user.Id, "WrongOldPassword", "NewPass", ct);
            Assert.False(failedResult);
        }

        // Should still be able to change password with correct current password
        var successResult = await _sut.ChangePasswordAsync(user.Id, originalPassword, newPassword, ct);
        Assert.True(successResult);

        // Verify new password works
        var authenticated = await _sut.AuthenticateAsync("change-pass-no-lockout", newPassword, ct);
        Assert.NotNull(authenticated);
    }

    [Fact]
    public async Task AuthenticateAsync_InterleavedFailuresAndSuccesses_NeverLocks()
    {
        var ct = TestContext.Current.CancellationToken;
        var correctPassword = "CorrectPassword123!";

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "interleaved-user",
            PasswordHash = _passwordService.HashPassword(correctPassword),
            Email = "interleaved@example.com",
            DisplayName = "Interleaved User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        // Interleave failed and successful attempts
        for (var i = 0; i < 20; i++)
        {
            // 5 failed attempts
            for (var j = 0; j < 5; j++)
            {
                var failedResult = await _sut.AuthenticateAsync("interleaved-user", "WrongPassword", ct);
                Assert.Null(failedResult);
            }

            // 1 successful attempt
            var successResult = await _sut.AuthenticateAsync("interleaved-user", correctPassword, ct);
            Assert.NotNull(successResult);
        }

        // Final successful login after 100 failed attempts total
        var finalResult = await _sut.AuthenticateAsync("interleaved-user", correctPassword, ct);
        Assert.NotNull(finalResult);
        Assert.Equal("interleaved-user", finalResult.Username);
    }
}
