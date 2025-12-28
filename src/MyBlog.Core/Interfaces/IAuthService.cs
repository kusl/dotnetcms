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
