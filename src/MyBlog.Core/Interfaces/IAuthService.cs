using MyBlog.Core.Models;

namespace MyBlog.Core.Interfaces;

public interface IAuthService
{
    Task<User?> AuthenticateAsync(
        string username, string password, CancellationToken cancellationToken = default);

    Task EnsureAdminUserAsync(CancellationToken cancellationToken = default);

    Task<bool> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);
}
