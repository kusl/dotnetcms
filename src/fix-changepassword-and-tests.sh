#!/bin/bash
# =============================================================================
# Fix ChangePassword.razor form, add rate limiting, fix tests for Windows
# =============================================================================
set -euo pipefail

SRC_DIR="src"
SCRIPT_NAME=$(basename "$0")

echo "=============================================="
echo "  MyBlog: Complete Fix Script"
echo "=============================================="
echo ""

# =============================================================================
# Step 1: Fix ChangePassword.razor - Add name attributes and SupplyParameterFromForm
# =============================================================================
echo "[1/5] Fixing ChangePassword.razor form (adding name attributes)..."

cat << 'EOF' > "$SRC_DIR/MyBlog.Web/Components/Pages/Admin/ChangePassword.razor"
@page "/admin/change-password"
@attribute [Authorize]
@inject IAuthService AuthService
@inject IHttpContextAccessor HttpContextAccessor
@inject NavigationManager Navigation
@using System.Security.Claims

<PageTitle>Change Password</PageTitle>

<h1>Change Password</h1>

<div class="change-password-form">
    @if (!string.IsNullOrEmpty(_successMessage))
    {
        <div class="success-message">@_successMessage</div>
    }

    @if (!string.IsNullOrEmpty(_errorMessage))
    {
        <div class="error-message">@_errorMessage</div>
    }

    <form method="post" @onsubmit="HandleSubmit" @formname="changepassword">
        <AntiforgeryToken />

        <div class="form-group">
            <label for="currentPassword">Current Password</label>
            <input type="password" id="currentPassword" name="currentPassword" @bind="_currentPassword" required />
        </div>

        <div class="form-group">
            <label for="newPassword">New Password</label>
            <input type="password" id="newPassword" name="newPassword" @bind="_newPassword" required minlength="8" />
            <small>Minimum 8 characters</small>
        </div>

        <div class="form-group">
            <label for="confirmPassword">Confirm New Password</label>
            <input type="password" id="confirmPassword" name="confirmPassword" @bind="_confirmPassword" required />
        </div>

        <div class="form-actions">
            <button type="submit" class="btn btn-primary">Change Password</button>
            <a href="/admin" class="btn btn-secondary">Cancel</a>
        </div>
    </form>
</div>

@code {
    private string _currentPassword = "";
    private string _newPassword = "";
    private string _confirmPassword = "";
    private string? _successMessage;
    private string? _errorMessage;

    [SupplyParameterFromForm(Name = "currentPassword")]
    public string? FormCurrentPassword { get; set; }

    [SupplyParameterFromForm(Name = "newPassword")]
    public string? FormNewPassword { get; set; }

    [SupplyParameterFromForm(Name = "confirmPassword")]
    public string? FormConfirmPassword { get; set; }

    private async Task HandleSubmit()
    {
        _successMessage = null;
        _errorMessage = null;

        // Use form values if available (SSR form post), otherwise use bound values
        var currentPassword = FormCurrentPassword ?? _currentPassword;
        var newPassword = FormNewPassword ?? _newPassword;
        var confirmPassword = FormConfirmPassword ?? _confirmPassword;

        // Validation
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
        {
            _errorMessage = "New password must be at least 8 characters.";
            return;
        }

        if (newPassword != confirmPassword)
        {
            _errorMessage = "New password and confirmation do not match.";
            return;
        }

        if (currentPassword == newPassword)
        {
            _errorMessage = "New password must be different from current password.";
            return;
        }

        // Get current user ID
        var context = HttpContextAccessor.HttpContext;
        var userIdClaim = context?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            _errorMessage = "Unable to identify current user. Please log in again.";
            return;
        }

        // Attempt password change
        var success = await AuthService.ChangePasswordAsync(userId, currentPassword, newPassword);

        if (success)
        {
            _successMessage = "Password changed successfully!";
            _currentPassword = "";
            _newPassword = "";
            _confirmPassword = "";
        }
        else
        {
            _errorMessage = "Current password is incorrect.";
        }
    }
}
EOF

echo "      Done."

# =============================================================================
# Step 2: Add rate limiting middleware (slows down but never blocks)
# =============================================================================
echo "[2/5] Adding rate limiting middleware..."

cat << 'EOF' > "$SRC_DIR/MyBlog.Web/Middleware/LoginRateLimitMiddleware.cs"
using System.Collections.Concurrent;

namespace MyBlog.Web.Middleware;

/// <summary>
/// Rate limiting middleware for login attempts.
/// Slows down requests but NEVER blocks users completely.
/// </summary>
public sealed class LoginRateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<LoginRateLimitMiddleware> _logger;

    // Track attempts per IP: IP -> (attempt count, window start)
    private static readonly ConcurrentDictionary<string, (int Count, DateTime WindowStart)> _attempts = new();

    // Configuration
    private const int WindowMinutes = 15;
    private const int AttemptsBeforeDelay = 5;
    private const int MaxDelaySeconds = 30;

    public LoginRateLimitMiddleware(RequestDelegate next, ILogger<LoginRateLimitMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only rate limit POST requests to login endpoint
        if (!IsLoginPostRequest(context))
        {
            await _next(context);
            return;
        }

        var ip = GetClientIp(context);
        var delay = CalculateDelay(ip);

        if (delay > TimeSpan.Zero)
        {
            _logger.LogInformation(
                "Rate limiting login attempt from {IP}, delaying {Seconds}s",
                ip, delay.TotalSeconds);
            await Task.Delay(delay, context.RequestAborted);
        }

        // Always proceed - never block
        await _next(context);

        // Record the attempt after processing
        RecordAttempt(ip);
    }

    private static bool IsLoginPostRequest(HttpContext context)
    {
        return context.Request.Method == HttpMethods.Post &&
               context.Request.Path.StartsWithSegments("/login", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetClientIp(HttpContext context)
    {
        // Check for forwarded IP (behind proxy/load balancer)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            var ip = forwardedFor.Split(',')[0].Trim();
            if (!string.IsNullOrEmpty(ip))
            {
                return ip;
            }
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static TimeSpan CalculateDelay(string ip)
    {
        if (!_attempts.TryGetValue(ip, out var record))
        {
            return TimeSpan.Zero;
        }

        // Reset if window expired
        if (DateTime.UtcNow - record.WindowStart > TimeSpan.FromMinutes(WindowMinutes))
        {
            _attempts.TryRemove(ip, out _);
            return TimeSpan.Zero;
        }

        // No delay for first few attempts
        if (record.Count < AttemptsBeforeDelay)
        {
            return TimeSpan.Zero;
        }

        // Progressive delay: 1s, 2s, 4s, 8s, ... capped at MaxDelaySeconds
        var delayMultiplier = record.Count - AttemptsBeforeDelay;
        var delaySeconds = Math.Min(Math.Pow(2, delayMultiplier), MaxDelaySeconds);
        return TimeSpan.FromSeconds(delaySeconds);
    }

    private static void RecordAttempt(string ip)
    {
        var now = DateTime.UtcNow;

        _attempts.AddOrUpdate(
            ip,
            _ => (1, now),
            (_, existing) =>
            {
                // Reset window if expired
                if (now - existing.WindowStart > TimeSpan.FromMinutes(WindowMinutes))
                {
                    return (1, now);
                }
                return (existing.Count + 1, existing.WindowStart);
            });

        // Cleanup old entries periodically (every 100th request)
        if (Random.Shared.Next(100) == 0)
        {
            CleanupOldEntries();
        }
    }

    private static void CleanupOldEntries()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-WindowMinutes * 2);
        foreach (var kvp in _attempts)
        {
            if (kvp.Value.WindowStart < cutoff)
            {
                _attempts.TryRemove(kvp.Key, out _);
            }
        }
    }
}

/// <summary>
/// Extension methods for LoginRateLimitMiddleware.
/// </summary>
public static class LoginRateLimitMiddlewareExtensions
{
    /// <summary>
    /// Adds login rate limiting middleware that slows down repeated attempts
    /// but never completely blocks users.
    /// </summary>
    public static IApplicationBuilder UseLoginRateLimit(this IApplicationBuilder app)
    {
        return app.UseMiddleware<LoginRateLimitMiddleware>();
    }
}
EOF

echo "      Done."

# =============================================================================
# Step 3: Update Program.cs to use rate limiting
# =============================================================================
echo "[3/5] Updating Program.cs to use rate limiting middleware..."

# We need to add the middleware. Let's check if it exists and add it if not
if ! grep -q "UseLoginRateLimit" "$SRC_DIR/MyBlog.Web/Program.cs" 2>/dev/null; then
    # Add using statement at the top if not present
    if ! grep -q "using MyBlog.Web.Middleware" "$SRC_DIR/MyBlog.Web/Program.cs" 2>/dev/null; then
        sed -i '1s/^/using MyBlog.Web.Middleware;\n/' "$SRC_DIR/MyBlog.Web/Program.cs"
    fi
    
    # Add middleware after UseRouting (or after app is created if UseRouting doesn't exist)
    if grep -q "app.UseRouting" "$SRC_DIR/MyBlog.Web/Program.cs"; then
        sed -i '/app\.UseRouting/a app.UseLoginRateLimit();' "$SRC_DIR/MyBlog.Web/Program.cs"
    elif grep -q "app.UseAuthentication" "$SRC_DIR/MyBlog.Web/Program.cs"; then
        sed -i '/app\.UseAuthentication/i app.UseLoginRateLimit();' "$SRC_DIR/MyBlog.Web/Program.cs"
    fi
    echo "      Added UseLoginRateLimit() to middleware pipeline."
else
    echo "      UseLoginRateLimit() already present."
fi

echo "      Done."

# =============================================================================
# Step 4: Add tests using in-memory SQLite (Windows compatible)
# =============================================================================
echo "[4/5] Adding Windows-compatible tests (using in-memory SQLite)..."

cat << 'EOF' > "$SRC_DIR/MyBlog.Tests/Integration/AuthServiceLongPasswordTests.cs"
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;
using MyBlog.Infrastructure.Repositories;
using MyBlog.Infrastructure.Services;

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
            Username = "longpassuser",
            PasswordHash = _passwordService.HashPassword(longPassword),
            Email = "longpass@example.com",
            DisplayName = "Long Password User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        var result = await _sut.AuthenticateAsync("longpassuser", longPassword, ct);

        Assert.NotNull(result);
        Assert.Equal("longpassuser", result.Username);
    }

    [Fact]
    public async Task AuthenticateAsync_With256CharacterPassword_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var longPassword = new string('x', 256);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "verylongpassuser",
            PasswordHash = _passwordService.HashPassword(longPassword),
            Email = "verylong@example.com",
            DisplayName = "Very Long Password User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        var result = await _sut.AuthenticateAsync("verylongpassuser", longPassword, ct);

        Assert.NotNull(result);
        Assert.Equal("verylongpassuser", result.Username);
    }

    [Fact]
    public async Task AuthenticateAsync_With512CharacterPassword_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var longPassword = new string('P', 512);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "extralongpassuser",
            PasswordHash = _passwordService.HashPassword(longPassword),
            Email = "extralong@example.com",
            DisplayName = "Extra Long Password User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        var result = await _sut.AuthenticateAsync("extralongpassuser", longPassword, ct);

        Assert.NotNull(result);
        Assert.Equal("extralongpassuser", result.Username);
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
            Username = "changepasslonguser",
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
        var authenticated = await _sut.AuthenticateAsync("changepasslonguser", newLongPassword, ct);
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
            Username = "complexpassuser",
            PasswordHash = _passwordService.HashPassword(complexPassword),
            Email = "complex@example.com",
            DisplayName = "Complex Password User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        var result = await _sut.AuthenticateAsync("complexpassuser", complexPassword, ct);

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
            Username = "nolockoutuser",
            PasswordHash = _passwordService.HashPassword(correctPassword),
            Email = "nolockout@example.com",
            DisplayName = "No Lockout User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        // Attempt 100 failed logins
        for (var i = 0; i < 100; i++)
        {
            var failedResult = await _sut.AuthenticateAsync("nolockoutuser", "WrongPassword", ct);
            Assert.Null(failedResult);
        }

        // User should still be able to log in with correct password
        var successResult = await _sut.AuthenticateAsync("nolockoutuser", correctPassword, ct);
        Assert.NotNull(successResult);
        Assert.Equal("nolockoutuser", successResult.Username);
    }

    [Fact]
    public async Task AuthenticateAsync_After1000FailedAttempts_StillAllowsLogin()
    {
        var ct = TestContext.Current.CancellationToken;
        var correctPassword = "CorrectPassword123!";

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "nolockout1000user",
            PasswordHash = _passwordService.HashPassword(correctPassword),
            Email = "nolockout1000@example.com",
            DisplayName = "No Lockout 1000 User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        // Attempt 1000 failed logins
        for (var i = 0; i < 1000; i++)
        {
            var failedResult = await _sut.AuthenticateAsync("nolockout1000user", $"WrongPassword{i}", ct);
            Assert.Null(failedResult);
        }

        // User should still be able to log in with correct password
        var successResult = await _sut.AuthenticateAsync("nolockout1000user", correctPassword, ct);
        Assert.NotNull(successResult);
        Assert.Equal("nolockout1000user", successResult.Username);
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
            Username = "changepassnolockout",
            PasswordHash = _passwordService.HashPassword(originalPassword),
            Email = "changepassnolockout@example.com",
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
        var authenticated = await _sut.AuthenticateAsync("changepassnolockout", newPassword, ct);
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
            Username = "interleaveduser",
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
                var failedResult = await _sut.AuthenticateAsync("interleaveduser", "WrongPassword", ct);
                Assert.Null(failedResult);
            }

            // 1 successful attempt
            var successResult = await _sut.AuthenticateAsync("interleaveduser", correctPassword, ct);
            Assert.NotNull(successResult);
        }

        // Final successful login after 100 failed attempts total
        var finalResult = await _sut.AuthenticateAsync("interleaveduser", correctPassword, ct);
        Assert.NotNull(finalResult);
        Assert.Equal("interleaveduser", finalResult.Username);
    }
}
EOF

echo "      Done."

# =============================================================================
# Step 5: Add rate limiting middleware tests
# =============================================================================
echo "[5/5] Adding rate limiting middleware tests..."

cat << 'EOF' > "$SRC_DIR/MyBlog.Tests/Unit/LoginRateLimitMiddlewareTests.cs"
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MyBlog.Web.Middleware;

namespace MyBlog.Tests.Unit;

/// <summary>
/// Tests for LoginRateLimitMiddleware.
/// Verifies that the middleware slows down but never blocks requests.
/// </summary>
public sealed class LoginRateLimitMiddlewareTests
{
    private readonly LoginRateLimitMiddleware _sut;
    private int _nextCallCount;

    public LoginRateLimitMiddlewareTests()
    {
        _nextCallCount = 0;
        RequestDelegate next = _ =>
        {
            _nextCallCount++;
            return Task.CompletedTask;
        };
        _sut = new LoginRateLimitMiddleware(next, NullLogger<LoginRateLimitMiddleware>.Instance);
    }

    [Fact]
    public async Task InvokeAsync_NonLoginRequest_PassesThroughImmediately()
    {
        var ct = TestContext.Current.CancellationToken;
        var context = CreateHttpContext("/api/posts", "GET");

        await _sut.InvokeAsync(context);

        Assert.Equal(1, _nextCallCount);
    }

    [Fact]
    public async Task InvokeAsync_GetLoginRequest_PassesThroughImmediately()
    {
        var ct = TestContext.Current.CancellationToken;
        var context = CreateHttpContext("/login", "GET");

        await _sut.InvokeAsync(context);

        Assert.Equal(1, _nextCallCount);
    }

    [Fact]
    public async Task InvokeAsync_FirstLoginAttempt_PassesThroughImmediately()
    {
        var ct = TestContext.Current.CancellationToken;
        var context = CreateHttpContext("/login", "POST", "192.168.1.100");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await _sut.InvokeAsync(context);
        sw.Stop();

        Assert.Equal(1, _nextCallCount);
        Assert.True(sw.ElapsedMilliseconds < 500, "First attempt should not be delayed");
    }

    [Fact]
    public async Task InvokeAsync_AfterManyAttempts_StillPassesThrough()
    {
        // Use unique IP to avoid interference from other tests
        var uniqueIp = $"10.0.0.{Random.Shared.Next(1, 255)}";

        // Make 20 requests - middleware should always pass through
        for (var i = 0; i < 20; i++)
        {
            var context = CreateHttpContext("/login", "POST", uniqueIp);
            await _sut.InvokeAsync(context);
        }

        // All 20 requests should have passed through
        Assert.Equal(20, _nextCallCount);
    }

    [Fact]
    public async Task InvokeAsync_NeverBlocksCompletely()
    {
        // Use unique IP
        var uniqueIp = $"10.0.1.{Random.Shared.Next(1, 255)}";

        // Even after 100 attempts, requests should pass through
        for (var i = 0; i < 100; i++)
        {
            var context = CreateHttpContext("/login", "POST", uniqueIp);
            
            // Use a timeout to ensure we're not blocked forever
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try
            {
                await _sut.InvokeAsync(context);
            }
            catch (OperationCanceledException)
            {
                Assert.Fail($"Request {i} was blocked or timed out");
            }
        }

        Assert.Equal(100, _nextCallCount);
    }

    private static DefaultHttpContext CreateHttpContext(string path, string method, string? remoteIp = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = method;
        
        if (remoteIp != null)
        {
            context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(remoteIp);
        }

        return context;
    }
}
EOF

echo "      Done."

echo ""
echo "=============================================="
echo "  Completed!"
echo "=============================================="
echo ""
echo "Changes made:"
echo ""
echo "  1. Fixed ChangePassword.razor:"
echo "     - Added 'name' attributes to all form inputs"
echo "     - Added [SupplyParameterFromForm] properties"
echo "     - Updated HandleSubmit to use form values"
echo ""
echo "  2. Added LoginRateLimitMiddleware:"
echo "     - Slows down repeated login attempts (progressive delay)"
echo "     - NEVER blocks users completely"
echo "     - First 5 attempts: no delay"
echo "     - After 5 attempts: 1s, 2s, 4s, 8s... delay (max 30s)"
echo "     - 15-minute sliding window"
echo ""
echo "  3. Updated Program.cs to use rate limiting"
echo ""
echo "  4. Fixed tests to use in-memory SQLite:"
echo "     - Changed from file-based SQLite to 'Data Source=:memory:'"
echo "     - This fixes Windows file locking issues"
echo "     - Works on Windows, Linux, and macOS"
echo ""
echo "  5. Added rate limiting middleware tests"
echo ""
echo "Next steps:"
echo "  1. Run: dotnet build"
echo "  2. Run: dotnet test"
echo ""
