namespace MyBlog.Core.Models;

/// <summary>Data transfer object for post listings.</summary>
public sealed record PostListItemDto(
    Guid Id,
    string Title,
    string Slug,
    string Summary,
    string AuthorDisplayName,
    DateTime? PublishedAtUtc,
    bool IsPublished);

/// <summary>Data transfer object for creating a new post.</summary>
public sealed record CreatePostDto(
    string Title,
    string Content,
    string Summary,
    bool IsPublished);

/// <summary>Data transfer object for updating a post.</summary>
public sealed record UpdatePostDto(
    string Title,
    string Content,
    string Summary,
    bool IsPublished);

/// <summary>Data transfer object for post details.</summary>
public sealed record PostDetailDto(
    Guid Id,
    string Title,
    string Slug,
    string Content,
    string Summary,
    string AuthorDisplayName,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? PublishedAtUtc,
    bool IsPublished);
