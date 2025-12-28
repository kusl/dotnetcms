#!/bin/bash
# =============================================================================
# MyBlog Upgrade Script
# Adds password change functionality, fixes deployment, generates tests & docs
# =============================================================================

set -e

# Detect the src directory
if [ -d "./src" ]; then
    SRC_DIR="./src"
elif [ -d "../src" ]; then
    SRC_DIR="../src"
else
    echo "Error: Cannot find src directory. Run this from the repository root."
    exit 1
fi

echo "=============================================="
echo "  MyBlog Upgrade Script"
echo "=============================================="
echo ""
echo "This script will:"
echo "  1. Add password change functionality to the website"
echo "  2. Fix the ERROR_FILE_IN_USE deployment issue"
echo "  3. Generate test cases for new features"
echo "  4. Create a comprehensive README"
echo ""
read -p "Continue? (y/N) " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "Aborted."
    exit 0
fi

# =============================================================================
# Step 1: Add IUserRepository.UpdateAsync and IAuthService.ChangePasswordAsync
# =============================================================================
echo ""
echo "[1/7] Adding password change interface methods..."

# Update IUserRepository.cs
cat << 'EOF' > "$SRC_DIR/MyBlog.Core/Interfaces/IUserRepository.cs"
using MyBlog.Core.Models;

namespace MyBlog.Core.Interfaces;

/// <summary>
/// Repository interface for user data access.
/// </summary>
public interface IUserRepository
{
    /// <summary>Gets a user by ID.</summary>
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Gets a user by username.</summary>
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>Checks if any users exist.</summary>
    Task<bool> AnyUsersExistAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates a new user.</summary>
    Task CreateAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing user.</summary>
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);
}
EOF

# Update IAuthService.cs
cat << 'EOF' > "$SRC_DIR/MyBlog.Core/Interfaces/IAuthService.cs"
using MyBlog.Core.Models;

namespace MyBlog.Core.Interfaces;

/// <summary>
/// Service interface for authentication operations.
/// </summary>
public interface IAuthService
{
    /// <summary>Attempts to authenticate a user.</summary>
    Task<User?> AuthenticateAsync(
        string username, string password, CancellationToken cancellationToken = default);

    /// <summary>Ensures the default admin user exists.</summary>
    Task EnsureAdminUserAsync(CancellationToken cancellationToken = default);

    /// <summary>Changes a user's password.</summary>
    /// <returns>True if password was changed, false if current password was incorrect.</returns>
    Task<bool> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);

    /// <summary>Resets a user's password without requiring the current password (admin function).</summary>
    Task ResetPasswordAsync(
        Guid userId,
        string newPassword,
        CancellationToken cancellationToken = default);
}
EOF

echo "      Done."

# =============================================================================
# Step 2: Update UserRepository implementation
# =============================================================================
echo "[2/7] Updating UserRepository with UpdateAsync..."

cat << 'EOF' > "$SRC_DIR/MyBlog.Infrastructure/Repositories/UserRepository.cs"
using Microsoft.EntityFrameworkCore;
using MyBlog.Core.Interfaces;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;

namespace MyBlog.Infrastructure.Repositories;

/// <summary>
/// SQLite implementation of the user repository.
/// </summary>
public sealed class UserRepository : IUserRepository
{
    private readonly BlogDbContext _context;

    /// <summary>Initializes a new instance of UserRepository.</summary>
    public UserRepository(BlogDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
        => await _context.Users.FirstOrDefaultAsync(
            u => u.Username.ToLower() == username.ToLower(), cancellationToken);

    /// <inheritdoc />
    public async Task<bool> AnyUsersExistAsync(CancellationToken cancellationToken = default)
        => await _context.Users.AnyAsync(cancellationToken);

    /// <inheritdoc />
    public async Task CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
EOF

echo "      Done."

# =============================================================================
# Step 3: Update AuthService implementation
# =============================================================================
echo "[3/7] Updating AuthService with ChangePasswordAsync..."

cat << 'EOF' > "$SRC_DIR/MyBlog.Infrastructure/Services/AuthService.cs"
using Microsoft.Extensions.Configuration;
using MyBlog.Core.Interfaces;
using MyBlog.Core.Models;

namespace MyBlog.Infrastructure.Services;

/// <summary>
/// Authentication service implementation.
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordService _passwordService;
    private readonly IConfiguration _configuration;

    /// <summary>Initializes a new instance of AuthService.</summary>
    public AuthService(
        IUserRepository userRepository,
        IPasswordService passwordService,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _passwordService = passwordService;
        _configuration = configuration;
    }

    /// <inheritdoc />
    public async Task<User?> AuthenticateAsync(
        string username, string password, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByUsernameAsync(username, cancellationToken);
        if (user is null)
        {
            return null;
        }

        return _passwordService.VerifyPassword(user.PasswordHash, password) ? user : null;
    }

    /// <inheritdoc />
    public async Task EnsureAdminUserAsync(CancellationToken cancellationToken = default)
    {
        if (await _userRepository.AnyUsersExistAsync(cancellationToken))
        {
            return;
        }

        var defaultPassword = Environment.GetEnvironmentVariable("MYBLOG_ADMIN_PASSWORD")
            ?? _configuration["Authentication:DefaultAdminPassword"]
            ?? "ChangeMe123!";

        var admin = new User
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            PasswordHash = _passwordService.HashPassword(defaultPassword),
            Email = "admin@localhost",
            DisplayName = "Administrator",
            CreatedAtUtc = DateTime.UtcNow
        };

        await _userRepository.CreateAsync(admin, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return false;
        }

        // Verify current password
        if (!_passwordService.VerifyPassword(user.PasswordHash, currentPassword))
        {
            return false;
        }

        // Update to new password
        user.PasswordHash = _passwordService.HashPassword(newPassword);
        await _userRepository.UpdateAsync(user, cancellationToken);

        return true;
    }

    /// <inheritdoc />
    public async Task ResetPasswordAsync(
        Guid userId,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            throw new InvalidOperationException($"User with ID {userId} not found.");
        }

        user.PasswordHash = _passwordService.HashPassword(newPassword);
        await _userRepository.UpdateAsync(user, cancellationToken);
    }
}
EOF

echo "      Done."

# =============================================================================
# Step 4: Create ChangePassword.razor page
# =============================================================================
echo "[4/7] Creating password change UI..."

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
            <input type="password" id="currentPassword" @bind="_currentPassword" required />
        </div>

        <div class="form-group">
            <label for="newPassword">New Password</label>
            <input type="password" id="newPassword" @bind="_newPassword" required minlength="8" />
            <small>Minimum 8 characters</small>
        </div>

        <div class="form-group">
            <label for="confirmPassword">Confirm New Password</label>
            <input type="password" id="confirmPassword" @bind="_confirmPassword" required />
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

    private async Task HandleSubmit()
    {
        _successMessage = null;
        _errorMessage = null;

        // Validation
        if (string.IsNullOrWhiteSpace(_newPassword) || _newPassword.Length < 8)
        {
            _errorMessage = "New password must be at least 8 characters.";
            return;
        }

        if (_newPassword != _confirmPassword)
        {
            _errorMessage = "New password and confirmation do not match.";
            return;
        }

        if (_currentPassword == _newPassword)
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
        var success = await AuthService.ChangePasswordAsync(userId, _currentPassword, _newPassword);

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
# Step 5: Update GitHub Actions workflow with AppOffline
# =============================================================================
echo "[5/7] Fixing deployment with AppOffline rule..."

cat << 'EOF' > "$SRC_DIR/.github/workflows/build-deploy.yml"
name: Build, Test, and Deploy

on:
  push:
    branches: ['**']
  pull_request:
    branches: ['**']

jobs:
  build-test:
    strategy:
      matrix:
        os: [ubuntu-latest, windows-latest, macos-latest]
    runs-on: ${{ matrix.os }}
    
    steps:
      - name: Checkout repository
        uses: actions/checkout@v4

      - name: Setup .NET 10
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore dependencies
        run: dotnet restore src/MyBlog.slnx

      - name: Build solution
        run: dotnet build src/MyBlog.slnx -c Release --no-restore

      - name: Run tests
        run: dotnet test src/MyBlog.slnx -c Release --no-build --logger trx --results-directory TestResults

      - name: Upload test results
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: test-results-${{ matrix.os }}
          path: TestResults
          retention-days: 7

  deploy:
    needs: build-test
    if: github.ref == 'refs/heads/main' || github.ref == 'refs/heads/master' || github.ref == 'refs/heads/develop'
    runs-on: windows-latest
    
    steps:
      - name: Checkout repository
        uses: actions/checkout@v4

      - name: Setup .NET 10
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Publish application
        run: dotnet publish src/MyBlog.Web/MyBlog.Web.csproj -c Release -o ./publish -r win-x86 --self-contained false

      - name: Deploy via WebDeploy
        shell: pwsh
        env:
          DEPLOY_SOURCE: ${{ github.workspace }}\publish
          DEPLOY_SITE: ${{ secrets.WEBSITE_NAME }}
          DEPLOY_HOST: ${{ secrets.SERVER_COMPUTER_NAME }}
          DEPLOY_USER: ${{ secrets.SERVER_USERNAME }}
          DEPLOY_PASSWORD: ${{ secrets.SERVER_PASSWORD }}
        run: |
          $msdeployPath = "C:\Program Files\IIS\Microsoft Web Deploy V3\msdeploy.exe"
          
          if (-not (Test-Path $msdeployPath)) {
            Write-Host "Installing Web Deploy..."
            choco install webdeploy -y --no-progress
          }
          
          Write-Host "Deploying to $env:DEPLOY_HOST..."
          Write-Host "Note: Using AppOffline rule to prevent file-in-use errors"

          $sourceArg = "-source:contentPath=$env:DEPLOY_SOURCE"
          $destArg = "-dest:contentPath=$env:DEPLOY_SITE,computerName=https://$($env:DEPLOY_HOST):8172/MsDeploy.axd?site=$env:DEPLOY_SITE,userName=$env:DEPLOY_USER,password=$env:DEPLOY_PASSWORD,AuthType='Basic'"
          
          # Key fix: Added -enableRule:AppOffline to stop the app during deployment
          # This creates app_offline.htm, waits for app to stop, deploys, then removes the file
          & $msdeployPath -verb:sync $sourceArg $destArg `
            -allowUntrusted `
            -enableRule:DoNotDeleteRule `
            -enableRule:AppOffline `
            -retryAttempts:3 `
            -retryInterval:3000
          
          if ($LASTEXITCODE -ne 0) {
            Write-Error "Deployment failed with exit code $LASTEXITCODE"
            exit 1
          }
          
          Write-Host "Deployment completed successfully!"
EOF

echo "      Done."

# =============================================================================
# Step 6: Generate test cases
# =============================================================================
echo "[6/7] Generating test cases for password change..."

cat << 'EOF' > "$SRC_DIR/MyBlog.Tests/Integration/PasswordChangeTests.cs"
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
EOF

echo "      Done."

# =============================================================================
# Step 7: Generate README
# =============================================================================
echo "[7/7] Generating comprehensive README..."

cat << 'EOF' > "$SRC_DIR/../README.md"
# MyBlog

A lightweight, self-hosted blogging platform built with .NET 10 and Blazor Server.

## Features

- **Markdown-based content**: Write posts in Markdown with live preview
- **Image management**: Upload and manage images stored in the database
- **Admin dashboard**: Manage posts, images, and settings
- **OpenTelemetry**: Built-in observability with file-based telemetry export
- **Cross-platform**: Runs on Windows, Linux, and macOS
- **CI/CD ready**: GitHub Actions workflow for automated testing and deployment

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later

### Running Locally

```bash
# Clone the repository
git clone https://github.com/yourusername/dotnetcms.git
cd dotnetcms/src

# Restore and run
dotnet restore MyBlog.slnx
cd MyBlog.Web
dotnet run
```

The application will start at `http://localhost:5000` (or the next available port).

### Default Credentials

- **Username**: `admin`
- **Password**: `ChangeMe123!` (or value of `MYBLOG_ADMIN_PASSWORD` environment variable)

> **Important**: The default password is only used when creating the initial admin user. Once the user exists, you must change the password through the website.

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `MYBLOG_ADMIN_PASSWORD` | Initial admin password (only used on first run) | `ChangeMe123!` |
| `ASPNETCORE_ENVIRONMENT` | Runtime environment | `Production` |

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=myblog.db"
  },
  "Authentication": {
    "SessionTimeoutMinutes": 30,
    "DefaultAdminPassword": "ChangeMe123!"
  },
  "Application": {
    "Title": "MyBlog"
  }
}
```

### Database Location

The SQLite database is stored in a platform-specific location:

| Platform | Path |
|----------|------|
| Linux | `~/.local/share/MyBlog/myblog.db` |
| macOS | `~/Library/Application Support/MyBlog/myblog.db` |
| Windows | `%LOCALAPPDATA%\MyBlog\myblog.db` |

## Admin Features

### Dashboard (`/admin`)

The admin dashboard provides an overview of your blog with quick access to all management features.

### Managing Posts

- **Create Post** (`/admin/posts/new`): Write a new blog post in Markdown
- **Edit Post** (`/admin/posts/edit/{id}`): Modify existing posts
- **Post List** (`/admin/posts`): View and manage all posts

### Managing Images

- **Upload Images** (`/admin/images`): Upload images to use in posts
- **Image Library**: Browse and delete uploaded images
- **Usage**: Reference images in Markdown using `/api/images/{id}`

### Changing Your Password

Navigate to `/admin/change-password` to change your admin password:

1. Enter your current password
2. Enter your new password (minimum 8 characters)
3. Confirm the new password
4. Click "Change Password"

> **Note**: The `MYBLOG_ADMIN_PASSWORD` environment variable only affects the initial password when the admin user is first created. It does not override existing passwords.

## Deployment

### GitHub Actions (Automated)

The repository includes a GitHub Actions workflow that:

1. Builds and tests on Windows, Linux, and macOS
2. Deploys to your server via WebDeploy (on main/master/develop branches)

#### Required Secrets

Set these in your repository settings under **Settings > Secrets and variables > Actions**:

| Secret | Description | Example |
|--------|-------------|---------|
| `WEBSITE_NAME` | IIS site name | `MyBlog` |
| `SERVER_COMPUTER_NAME` | Server hostname | `myserver.example.com` |
| `SERVER_USERNAME` | WebDeploy username | `deploy-user` |
| `SERVER_PASSWORD` | WebDeploy password | (your password) |

#### Repository Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `MYBLOG_ADMIN_PASSWORD` | Initial admin password | (strong password) |

> **Note**: `MYBLOG_ADMIN_PASSWORD` should be set as a **secret**, not a variable, if you want it to remain hidden in logs.

### Manual Deployment

```bash
# Publish for Windows
dotnet publish src/MyBlog.Web/MyBlog.Web.csproj -c Release -o ./publish -r win-x64

# Publish for Linux
dotnet publish src/MyBlog.Web/MyBlog.Web.csproj -c Release -o ./publish -r linux-x64
```

Copy the contents of `./publish` to your server.

### IIS Configuration

1. Install the [.NET 10 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/10.0)
2. Create a new IIS site pointing to your publish folder
3. Set the Application Pool to "No Managed Code"
4. Ensure the Application Pool identity has write access to the database directory

## Troubleshooting

### ERROR_FILE_IN_USE During Deployment

This occurs when the application is running and DLLs are locked.

**Solution**: The workflow now includes `-enableRule:AppOffline` which automatically:
1. Creates `app_offline.htm` to stop the application
2. Waits for the app to release file locks
3. Deploys the new files
4. Removes `app_offline.htm` to restart the app

### Password Not Changing After Setting MYBLOG_ADMIN_PASSWORD

The environment variable only works when **no users exist** in the database.

**To reset with a new password**:

1. Stop the application
2. Delete the database file (see Database Location above)
3. Set `MYBLOG_ADMIN_PASSWORD` to your desired password
4. Start the application

Or, log in with the current password and use `/admin/change-password`.

### Database Locked Errors

SQLite can have locking issues with concurrent access.

**Solutions**:
- Ensure only one instance of the application is running
- Check that no database tools have the file open
- Verify file permissions on the database directory

## Development

### Running Tests

```bash
cd src
dotnet test MyBlog.slnx
```

### Project Structure

```
src/
├── MyBlog.Core/           # Domain models and interfaces
├── MyBlog.Infrastructure/ # Data access, services
├── MyBlog.Web/           # Blazor Server application
└── MyBlog.Tests/         # xUnit test project
```

### Adding New Features

1. Define interfaces in `MyBlog.Core/Interfaces`
2. Implement in `MyBlog.Infrastructure/Services`
3. Add UI in `MyBlog.Web/Components/Pages`
4. Write tests in `MyBlog.Tests`

## API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/images/{id}` | GET | Retrieve an image by ID |

## License

MIT License - see [LICENSE](LICENSE) for details.

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request
EOF

echo "      Done."

# =============================================================================
# Complete!
# =============================================================================
echo ""
echo "=============================================="
echo "  Upgrade Complete!"
echo "=============================================="
echo ""
echo "Changes made:"
echo "  ✓ Added ChangePasswordAsync to IAuthService"
echo "  ✓ Added UpdateAsync to IUserRepository"
echo "  ✓ Created /admin/change-password page"
echo "  ✓ Fixed deployment with AppOffline rule"
echo "  ✓ Generated password change test cases"
echo "  ✓ Created comprehensive README.md"
echo ""
echo "Next steps:"
echo ""
echo "  1. Rebuild the solution:"
echo "     cd $SRC_DIR && dotnet build MyBlog.slnx"
echo ""
echo "  2. Run tests:"
echo "     dotnet test MyBlog.slnx"
echo ""
echo "  3. Commit and push changes"
echo ""
echo "  4. After deployment, log in and visit:"
echo "     /admin/change-password"
echo ""
echo "=============================================="
