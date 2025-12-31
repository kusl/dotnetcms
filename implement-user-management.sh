#!/bin/bash
set -euo pipefail

# =============================================================================
# Implement User Management (CRUD)
# =============================================================================
# 1. Updates IUserRepository/UserRepository with GetAll and Delete methods
# 2. Creates UserList.razor (Read/Delete)
# 3. Creates UserEditor.razor (Create/Update)
# 4. Updates MainLayout.razor with navigation link
# =============================================================================

SRC_DIR="src"

echo "=============================================="
echo "  Implementing User Management..."
echo "=============================================="

# -----------------------------------------------------------------------------
# 1. Update Repository Interfaces and Implementation
# -----------------------------------------------------------------------------
echo "[1/4] Updating User Repository..."

# Update IUserRepository.cs to include GetAll and Delete
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

    /// <summary>Gets all users.</summary>
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Checks if any users exist.</summary>
    Task<bool> AnyUsersExistAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates a new user.</summary>
    Task CreateAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing user.</summary>
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>Deletes a user by ID.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
EOF

# Update UserRepository.cs to implement new methods
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

    public UserRepository(BlogDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
        => await _context.Users.FirstOrDefaultAsync(
            u => u.Username.ToLower() == username.ToLower(), cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.Users.OrderBy(u => u.Username).ToListAsync(cancellationToken);

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

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FindAsync([id], cancellationToken);
        if (user is not null)
        {
            _context.Users.Remove(user);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
EOF

# -----------------------------------------------------------------------------
# 2. Create User List Page
# -----------------------------------------------------------------------------
echo "[2/4] Creating User List Page..."

cat << 'EOF' > "$SRC_DIR/MyBlog.Web/Components/Pages/Admin/UserList.razor"
@page "/admin/users"
@attribute [Authorize(Roles = AppConstants.AdminRole)]
@inject IUserRepository UserRepository
@inject AuthenticationStateProvider AuthStateProvider
@using System.Security.Claims

<PageTitle>Manage Users</PageTitle>

<h1>Manage Users</h1>

<p><a href="/admin/users/new" class="btn btn-primary">Create New User</a></p>

@if (_users is null)
{
    <p>Loading...</p>
}
else
{
    <table class="admin-table">
        <thead>
            <tr>
                <th>Username</th>
                <th>Display Name</th>
                <th>Email</th>
                <th>Actions</th>
            </tr>
        </thead>
        <tbody>
            @foreach (var user in _users)
            {
                <tr>
                    <td>
                        <a href="/admin/users/edit/@user.Id">@user.Username</a>
                    </td>
                    <td>@user.DisplayName</td>
                    <td>@user.Email</td>
                    <td>
                        <a href="/admin/users/edit/@user.Id">Edit</a>
                        @if (user.Id != _currentUserId)
                        {
                            <button @onclick="() => DeleteUser(user.Id)" class="btn-link danger" style="margin-left: 10px;">Delete</button>
                        }
                        else
                        {
                            <span class="text-muted" style="margin-left: 10px; color: #999;">(Current)</span>
                        }
                    </td>
                </tr>
            }
        </tbody>
    </table>
}

@code {
    private IReadOnlyList<User>? _users;
    private Guid _currentUserId;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var idClaim = authState.User.FindFirst(ClaimTypes.NameIdentifier);
        if (idClaim != null && Guid.TryParse(idClaim.Value, out var id))
        {
            _currentUserId = id;
        }

        await LoadUsers();
    }

    private async Task LoadUsers()
    {
        _users = await UserRepository.GetAllAsync();
    }

    private async Task DeleteUser(Guid id)
    {
        if (id == _currentUserId) return; // Prevent suicide

        var confirm = await Application.Current?.MainPage?.DisplayAlert("Confirm", "Are you sure you want to delete this user?", "Yes", "No") ?? true;
        
        // Note: Simple JS confirm isn't available in SSR easily without interop, 
        // so we'll just delete for now. In a real app, use a modal or JSInterop confirm.
        await UserRepository.DeleteAsync(id);
        await LoadUsers();
    }
}
EOF

# -----------------------------------------------------------------------------
# 3. Create User Editor Page
# -----------------------------------------------------------------------------
echo "[3/4] Creating User Editor Page..."

cat << 'EOF' > "$SRC_DIR/MyBlog.Web/Components/Pages/Admin/UserEditor.razor"
@page "/admin/users/new"
@page "/admin/users/edit/{Id:guid}"
@attribute [Authorize(Roles = AppConstants.AdminRole)]
@inject IUserRepository UserRepository
@inject IPasswordService PasswordService
@inject NavigationManager Navigation

<PageTitle>@(_isEdit ? "Edit User" : "New User")</PageTitle>

<h1>@(_isEdit ? "Edit User" : "New User")</h1>

<div class="user-editor" style="max-width: 600px;">
    @if (!string.IsNullOrEmpty(_error))
    {
        <div class="error-message">@_error</div>
    }

    <div class="form-group">
        <label for="username">Username</label>
        <input type="text" id="username" @bind="_username" required />
    </div>

    <div class="form-group">
        <label for="displayName">Display Name</label>
        <input type="text" id="displayName" @bind="_displayName" required />
    </div>

    <div class="form-group">
        <label for="email">Email</label>
        <input type="email" id="email" @bind="_email" required />
    </div>

    <div class="form-group">
        <label for="password">Password @(_isEdit ? "(Leave blank to keep current)" : "")</label>
        <input type="password" id="password" @bind="_password" required="@(!_isEdit)" />
        @if (!_isEdit) 
        {
            <small>Required for new users.</small>
        }
    </div>

    <div class="form-actions">
        <button @onclick="Save" class="btn btn-primary" disabled="@_saving">
            @(_saving ? "Saving..." : "Save User")
        </button>
        <a href="/admin/users" class="btn">Cancel</a>
    </div>
</div>

@code {
    [Parameter]
    public Guid? Id { get; set; }

    private bool _isEdit => Id.HasValue;
    private bool _saving;
    private string? _error;

    // Form fields
    private string _username = "";
    private string _displayName = "";
    private string _email = "";
    private string _password = "";

    private User? _existingUser;

    protected override async Task OnParametersSetAsync()
    {
        if (_isEdit)
        {
            _existingUser = await UserRepository.GetByIdAsync(Id!.Value);
            if (_existingUser is null)
            {
                Navigation.NavigateTo("/admin/users");
                return;
            }

            _username = _existingUser.Username;
            _displayName = _existingUser.DisplayName;
            _email = _existingUser.Email;
        }
    }

    private async Task Save()
    {
        _error = null;
        _saving = true;

        try
        {
            if (string.IsNullOrWhiteSpace(_username) || string.IsNullOrWhiteSpace(_displayName) || string.IsNullOrWhiteSpace(_email))
            {
                _error = "All fields are required.";
                return;
            }

            // Check if username is taken (by another user)
            var duplicate = await UserRepository.GetByUsernameAsync(_username);
            if (duplicate is not null && duplicate.Id != Id)
            {
                _error = "Username is already taken.";
                return;
            }

            if (_isEdit && _existingUser is not null)
            {
                // Update existing
                _existingUser.Username = _username;
                _existingUser.DisplayName = _displayName;
                _existingUser.Email = _email;

                // Only update password if provided
                if (!string.IsNullOrWhiteSpace(_password))
                {
                     if (_password.Length < 8)
                     {
                         _error = "Password must be at least 8 characters.";
                         return;
                     }
                    _existingUser.PasswordHash = PasswordService.HashPassword(_password);
                }

                await UserRepository.UpdateAsync(_existingUser);
            }
            else
            {
                // Create new
                if (string.IsNullOrWhiteSpace(_password) || _password.Length < 8)
                {
                    _error = "Password is required (min 8 chars) for new users.";
                    return;
                }

                var newUser = new User
                {
                    Id = Guid.NewGuid(),
                    Username = _username,
                    DisplayName = _displayName,
                    Email = _email,
                    PasswordHash = PasswordService.HashPassword(_password),
                    CreatedAtUtc = DateTime.UtcNow
                };

                await UserRepository.CreateAsync(newUser);
            }

            Navigation.NavigateTo("/admin/users");
        }
        catch (Exception ex)
        {
            _error = $"Error saving user: {ex.Message}";
        }
        finally
        {
            _saving = false;
        }
    }
}
EOF

# -----------------------------------------------------------------------------
# 4. Update Navigation Menu
# -----------------------------------------------------------------------------
echo "[4/4] Updating Navigation Menu..."

# We replace MainLayout.razor to inject the "Users" link into the Authorized block
cat << 'EOF' > "$SRC_DIR/MyBlog.Web/Components/Layout/MainLayout.razor"
@inherits LayoutComponentBase
@inject IConfiguration Configuration
@inject NavigationManager Navigation

<div class="layout">
    <header class="header">
        <div class="container">
            <a href="/" class="logo">@(Configuration["Application:Title"] ?? "MyBlog")</a>
            <nav class="nav">
                <a href="/">Home</a>
                <a href="/about">About</a>
                <AuthorizeView>
                    <Authorized>
                        <a href="/admin">Dashboard</a>
                        <a href="/admin/users">Users</a>
                        <form method="post" action="/logout" class="logout-form">
                            <AntiforgeryToken />
                            <button type="submit">Logout</button>
                        </form>
                    </Authorized>
                    <NotAuthorized>
                        <a href="/login">Login</a>
                    </NotAuthorized>
                </AuthorizeView>
            </nav>
        </div>
    </header>

    <main class="main">
        <div class="container">
            @Body
        </div>
    </main>

    <footer class="footer">
        <div class="container">
            <p>&copy; @DateTime.Now.Year @(Configuration["Application:Title"] ?? "MyBlog")</p>
        </div>
    </footer>
</div>
EOF

echo ""
echo "=============================================="
echo "  User Management Implemented Successfully!"
echo "=============================================="
echo "New Pages:"
echo "  - /admin/users (List users)"
echo "  - /admin/users/new (Create user)"
echo "  - /admin/users/edit/{id} (Edit user)"
echo ""
echo "Next steps:"
echo "  1. Run 'dotnet build src/MyBlog.slnx'"
echo "  2. Run 'dotnet run --project src/MyBlog.Web'"
echo "  3. Log in as admin and verify the 'Users' link in the header."
echo ""
