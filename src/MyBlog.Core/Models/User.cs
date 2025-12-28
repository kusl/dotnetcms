namespace MyBlog.Core.Models;

/// <summary>
/// Represents a user who can create and manage blog posts.
/// </summary>
public sealed class User
{
    /// <summary>Gets or sets the unique identifier for the user.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the unique username for authentication.</summary>
    public required string Username { get; set; }

    /// <summary>Gets or sets the hashed password.</summary>
    public required string PasswordHash { get; set; }

    /// <summary>Gets or sets the user's email address.</summary>
    public required string Email { get; set; }

    /// <summary>Gets or sets the display name shown on posts.</summary>
    public required string DisplayName { get; set; }

    /// <summary>Gets or sets when the user account was created.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Navigation property for posts authored by this user.</summary>
    public ICollection<Post> Posts { get; set; } = [];

    /// <summary>Navigation property for images uploaded by this user.</summary>
    public ICollection<Image> UploadedImages { get; set; } = [];
}
