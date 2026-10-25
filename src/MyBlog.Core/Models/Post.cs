namespace MyBlog.Core.Models;

public sealed class Post
{
    public Guid Id { get; set; }

    public required string Title { get; set; }

    public required string Slug { get; set; }

    public required string Content { get; set; }

    public required string Summary { get; set; }

    public Guid AuthorId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public DateTime? PublishedAtUtc { get; set; }

    public bool IsPublished { get; set; }

    public User? Author { get; set; }

    public ICollection<Image> Images { get; set; } = [];
}
