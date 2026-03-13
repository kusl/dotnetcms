using Microsoft.AspNetCore.Identity;
using MyBlog.Core.Interfaces;
using MyBlog.Core.Models;

namespace MyBlog.Infrastructure.Services;

/// <summary>
/// Password hashing service using ASP.NET Core Identity's PasswordHasher.
/// Accepts an injected IPasswordHasher&lt;User&gt; for proper DI integration,
/// allowing global configuration of hashing options via IServiceCollection.
/// </summary>
public sealed class PasswordService : IPasswordService
{
    private readonly IPasswordHasher<User> _hasher;

    /// <summary>
    /// DI constructor. Use this when resolving via the service container.
    /// </summary>
    public PasswordService(IPasswordHasher<User> hasher)
    {
        _hasher = hasher;
    }

    /// <summary>
    /// Parameterless constructor for backward compatibility (tests, standalone usage).
    /// </summary>
    public PasswordService() : this(new PasswordHasher<User>())
    {
    }

    /// <inheritdoc />
    public string HashPassword(string password) => _hasher.HashPassword(null!, password);

    /// <inheritdoc />
    public bool VerifyPassword(string hashedPassword, string providedPassword)
    {
        var result = _hasher.VerifyHashedPassword(null!, hashedPassword, providedPassword);
        return result == PasswordVerificationResult.Success ||
               result == PasswordVerificationResult.SuccessRehashNeeded;
    }
}
