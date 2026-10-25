namespace MyBlog.Core.Models;

public sealed class User
{
    public Guid Id { get; set; }

    public required string Username { get; set; }

    public required string PasswordHash { get; set; }

    public required string Email { get; set; }

    public required string DisplayName { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public ICollection<Post> Posts { get; set; } = [];

    public ICollection<Image> UploadedImages { get; set; } = [];
}
