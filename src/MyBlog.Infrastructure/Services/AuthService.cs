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
