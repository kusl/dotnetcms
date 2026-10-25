namespace MyBlog.Core.Models;

public sealed record PostListItemDto(
    Guid Id,
    string Title,
    string Slug,
    string Summary,
    string AuthorDisplayName,
    DateTime? PublishedAtUtc,
    bool IsPublished);

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
