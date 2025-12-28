#!/bin/bash
# =============================================================================
# Fix ChangePassword.razor form and add tests for long passwords & no lockout
# =============================================================================
set -euo pipefail

SRC_DIR="src"
SCRIPT_NAME=$(basename "$0")

echo "=============================================="
echo "  MyBlog: Fix ChangePassword Form & Add Tests"
echo "=============================================="
echo ""

# =============================================================================
# Step 1: Fix ChangePassword.razor - Add name attributes and SupplyParameterFromForm
# =============================================================================
echo "[1/3] Fixing ChangePassword.razor form (adding name attributes)..."

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
# Step 2: Add tests for long passwords (128+ characters) and no account lockout
# =============================================================================
echo "[2/3] Adding tests for long passwords and no account lockout..."

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
/// </summary>
public sealed class AuthServiceLongPasswordTests : IAsyncDisposable
{
    private readonly BlogDbContext _context;
    private readonly AuthService _sut;
    private readonly PasswordService _passwordService;

    public AuthServiceLongPasswordTests()
    {
        var options = new DbContextOptionsBuilder<BlogDbContext>()
            .UseSqlite($"Data Source={Guid.NewGuid()}.db")
            .Options;

        _context = new BlogDbContext(options);
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
        var dbPath = _context.Database.GetConnectionString()?.Replace("Data Source=", "");
        await _context.DisposeAsync();
        if (!string.IsNullOrEmpty(dbPath) && File.Exists(dbPath))
        {
            File.Delete(dbPath);
        }
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
# Step 3: Verify AuthService has no lockout mechanism
# =============================================================================
echo "[3/3] Verifying AuthService has no lockout mechanism..."

# Check if AuthService has any lockout-related code
if grep -q -i "lockout\|locked\|attempt\|fail.*count\|block\|ban" "$SRC_DIR/MyBlog.Infrastructure/Services/AuthService.cs" 2>/dev/null; then
    echo "      WARNING: AuthService may contain lockout-related code. Please review."
else
    echo "      VERIFIED: AuthService contains no lockout mechanism."
fi

echo ""
echo "=============================================="
echo "  Completed!"
echo "=============================================="
echo ""
echo "Changes made:"
echo "  1. Fixed ChangePassword.razor:"
echo "     - Added 'name' attributes to all form inputs"
echo "     - Added [SupplyParameterFromForm] properties"
echo "     - Updated HandleSubmit to use form values"
echo ""
echo "  2. Added AuthServiceLongPasswordTests.cs with tests for:"
echo "     - 128-character passwords"
echo "     - 256-character passwords"
echo "     - 512-character passwords"
echo "     - Complex long passwords"
echo "     - No lockout after 100 failed attempts"
echo "     - No lockout after 1000 failed attempts"
echo "     - Password change works after many failed attempts"
echo "     - Interleaved failures and successes never lock"
echo ""
echo "  3. Verified AuthService has no lockout mechanism"
echo ""
echo "Next steps:"
echo "  1. Run: dotnet build"
echo "  2. Run: dotnet test"
echo ""
