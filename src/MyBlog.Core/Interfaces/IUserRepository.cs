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
