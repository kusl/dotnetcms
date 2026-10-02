using MyBlog.Core.Models;

namespace MyBlog.Core.Interfaces;

public interface IPostRepository
{
    Task<(IReadOnlyList<PostListItemDto> Posts, int TotalCount)> GetPublishedPostsAsync(
        int page, int pageSize, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PostListItemDto>> GetAllPostsAsync(
        CancellationToken cancellationToken = default);

    Task<PostDetailDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<Post?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Post> CreateAsync(Post post, CancellationToken cancellationToken = default);

    Task UpdateAsync(Post post, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<int> GetCountAsync(CancellationToken cancellationToken = default);

    Task<int> GetPublishedCountAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PostListItemDto>> GetRecentPostsAsync(
        int count, CancellationToken cancellationToken = default);

    Task<bool> IsSlugTakenAsync(string slug, Guid? excludePostId = null, CancellationToken cancellationToken = default);
}
