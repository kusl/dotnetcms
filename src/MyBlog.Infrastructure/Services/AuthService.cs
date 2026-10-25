using Microsoft.Extensions.Configuration;
using MyBlog.Core.Interfaces;
using MyBlog.Core.Models;

namespace MyBlog.Infrastructure.Services;

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordService _passwordService;
    private readonly IConfiguration _configuration;

    public AuthService(
        IUserRepository userRepository,
        IPasswordService passwordService,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _passwordService = passwordService;
        _configuration = configuration;
    }

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

        if (!_passwordService.VerifyPassword(user.PasswordHash, currentPassword))
        {
            return false;
        }

        user.PasswordHash = _passwordService.HashPassword(newPassword);
        await _userRepository.UpdateAsync(user, cancellationToken);

        return true;
    }
}
