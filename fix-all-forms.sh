#!/bin/bash
# =============================================================================
# Fix all forms in MyBlog - Add name attributes for Blazor SSR form submissions
# =============================================================================
# This script fixes the missing 'name' attribute issue on form inputs.
# In Blazor Server Static SSR, forms using method="post" require the 'name'
# attribute for the browser to include field values in the POST data.
# The @bind directive alone only works for interactive Blazor components.
# =============================================================================
set -euo pipefail

SRC_DIR="src"

echo "=============================================="
echo "  MyBlog: Fix All Forms Script"
echo "=============================================="
echo ""

# =============================================================================
# Step 1: Fix Login.razor - Add name attributes and SupplyParameterFromForm
# =============================================================================
echo "[1/2] Fixing Login.razor form..."

cat << 'EOF' > "$SRC_DIR/MyBlog.Web/Components/Pages/Login.razor"
@page "/login"
@inject IAuthService AuthService
@inject NavigationManager Navigation
@inject IHttpContextAccessor HttpContextAccessor
@using System.Security.Claims
@using Microsoft.AspNetCore.Authentication
@using Microsoft.AspNetCore.Authentication.Cookies

<PageTitle>Login</PageTitle>

<div class="login-page">
    <h1>Login</h1>

    @if (!string.IsNullOrEmpty(_error))
    {
        <div class="error-message">@_error</div>
    }

    <form method="post" @onsubmit="HandleLogin" @formname="login">
        <AntiforgeryToken />
        
        <div class="form-group">
            <label for="username">Username</label>
            <input type="text" id="username" name="username" @bind="_username" required />
        </div>

        <div class="form-group">
            <label for="password">Password</label>
            <input type="password" id="password" name="password" @bind="_password" required />
        </div>

        <button type="submit" class="btn btn-primary">Login</button>
    </form>
</div>

@code {
    private string _username = "";
    private string _password = "";
    private string? _error;

    [SupplyParameterFromQuery]
    public string? ReturnUrl { get; set; }

    [SupplyParameterFromForm(Name = "username")]
    public string? FormUsername { get; set; }

    [SupplyParameterFromForm(Name = "password")]
    public string? FormPassword { get; set; }

    private async Task HandleLogin()
    {
        // Use form values if available (SSR form post), otherwise use bound values
        var username = FormUsername ?? _username;
        var password = FormPassword ?? _password;

        var user = await AuthService.AuthenticateAsync(username, password);
        
        if (user is null)
        {
            _error = "Invalid username or password";
            return;
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new("DisplayName", user.DisplayName)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var context = HttpContextAccessor.HttpContext!;
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        Navigation.NavigateTo(ReturnUrl ?? "/admin", forceLoad: true);
    }
}
EOF

echo "      Done."

# =============================================================================
# Step 2: Fix ChangePassword.razor - Add name attributes and SupplyParameterFromForm
# =============================================================================
echo "[2/2] Fixing ChangePassword.razor form..."

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
            // Clear form values as well
            FormCurrentPassword = null;
            FormNewPassword = null;
            FormConfirmPassword = null;
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
# Summary
# =============================================================================
echo ""
echo "=============================================="
echo "  All forms fixed!"
echo "=============================================="
echo ""
echo "Fixed files:"
echo "  - src/MyBlog.Web/Components/Pages/Login.razor"
echo "  - src/MyBlog.Web/Components/Pages/Admin/ChangePassword.razor"
echo ""
echo "Note: The PostEditor.razor and ImageManager.razor pages use"
echo "@onclick handlers (interactive Blazor), not method='post' forms,"
echo "so they do not need the 'name' attribute fix."
echo ""
echo "The fix adds:"
echo "  1. name='fieldname' attributes to all <input> elements"
echo "  2. [SupplyParameterFromForm] properties to receive POST data"
echo "  3. Logic to use form values when available (SSR) or bound values (interactive)"
echo ""
