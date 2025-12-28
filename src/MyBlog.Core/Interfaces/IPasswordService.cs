namespace MyBlog.Core.Interfaces;

/// <summary>
/// Service interface for password hashing and verification.
/// </summary>
public interface IPasswordService
{
    /// <summary>Hashes a plain text password.</summary>
    string HashPassword(string password);

    /// <summary>Verifies a password against a hash.</summary>
    bool VerifyPassword(string hashedPassword, string providedPassword);
}
