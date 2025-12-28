namespace MyBlog.Core.Models;

/// <summary>
/// Represents a blog post with Markdown content.
/// </summary>
public sealed class Post
{
    /// <summary>Gets or sets the unique identifier for the post.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the post title.</summary>
    public required string Title { get; set; }

    /// <summary>Gets or sets the URL-friendly slug.</summary>
    public required string Slug { get; set; }

    /// <summary>Gets or sets the Markdown content of the post.</summary>
    public required string Content { get; set; }

    /// <summary>Gets or sets a brief summary for listings.</summary>
    public required string Summary { get; set; }

    /// <summary>Gets or sets the author's user ID.</summary>
    public Guid AuthorId { get; set; }

    /// <summary>Gets or sets when the post was created.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Gets or sets when the post was last updated.</summary>
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>Gets or sets when the post was published (null if draft).</summary>
    public DateTime? PublishedAtUtc { get; set; }

    /// <summary>Gets or sets whether the post is publicly visible.</summary>
    public bool IsPublished { get; set; }

    /// <summary>Navigation property for the author.</summary>
    public User? Author { get; set; }

    /// <summary>Navigation property for attached images.</summary>
    public ICollection<Image> Images { get; set; } = [];
}
