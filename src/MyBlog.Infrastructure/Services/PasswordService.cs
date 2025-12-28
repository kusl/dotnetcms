using Microsoft.AspNetCore.Identity;
using MyBlog.Core.Interfaces;
using MyBlog.Core.Models;

namespace MyBlog.Infrastructure.Services;

/// <summary>
/// Password hashing service using ASP.NET Core Identity's PasswordHasher.
/// </summary>
public sealed class PasswordService : IPasswordService
{
    private readonly PasswordHasher<User> _hasher = new();

    /// <inheritdoc />
    public string HashPassword(string password)
    {
        return _hasher.HashPassword(null!, password);
    }

    /// <inheritdoc />
    public bool VerifyPassword(string hashedPassword, string providedPassword)
    {
        var result = _hasher.VerifyHashedPassword(null!, hashedPassword, providedPassword);
        return result == PasswordVerificationResult.Success ||
               result == PasswordVerificationResult.SuccessRehashNeeded;
    }
}
