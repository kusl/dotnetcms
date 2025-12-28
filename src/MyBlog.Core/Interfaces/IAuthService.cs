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
}
