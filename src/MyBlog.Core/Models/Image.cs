namespace MyBlog.Core.Models;

public sealed class Image
{
    public Guid Id { get; set; }

    public required string FileName { get; set; }

    public required string ContentType { get; set; }

    public required byte[] Data { get; set; }

    public Guid? PostId { get; set; }

    public DateTime UploadedAtUtc { get; set; }

    public Guid UploadedByUserId { get; set; }

    public Post? Post { get; set; }

    public User? UploadedBy { get; set; }
}
