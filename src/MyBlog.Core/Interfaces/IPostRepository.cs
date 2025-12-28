using MyBlog.Core.Models;

namespace MyBlog.Core.Interfaces;

/// <summary>
/// Repository interface for post operations.
/// </summary>
public interface IPostRepository
{
    /// <summary>Gets a paginated list of published posts.</summary>
    Task<(IReadOnlyList<PostListItemDto> Posts, int TotalCount)> GetPublishedPostsAsync(
        int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Gets all posts for admin view.</summary>
    Task<IReadOnlyList<PostListItemDto>> GetAllPostsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Gets a post by its slug.</summary>
    Task<PostDetailDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>Gets a post by its ID.</summary>
    Task<Post?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates a new post.</summary>
    Task<Post> CreateAsync(Post post, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing post.</summary>
    Task UpdateAsync(Post post, CancellationToken cancellationToken = default);

    /// <summary>Deletes a post by ID.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Gets the total count of posts.</summary>
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets recent posts for dashboard.</summary>
    Task<IReadOnlyList<PostListItemDto>> GetRecentPostsAsync(
        int count, CancellationToken cancellationToken = default);
}
