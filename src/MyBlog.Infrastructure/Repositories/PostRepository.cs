using Microsoft.EntityFrameworkCore;
using MyBlog.Core.Interfaces;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;

namespace MyBlog.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of the post repository.
/// </summary>
public sealed class PostRepository : IPostRepository
{
    private readonly BlogDbContext _context;

    /// <summary>Initializes a new instance of PostRepository.</summary>
    public PostRepository(BlogDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<PostListItemDto> Posts, int TotalCount)> GetPublishedPostsAsync(
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Posts
            .AsNoTracking()
            .Where(p => p.IsPublished)
            .OrderByDescending(p => p.PublishedAtUtc);

        var totalCount = await query.CountAsync(cancellationToken);

        var posts = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(p => p.Author)
            .Select(p => new PostListItemDto(
                p.Id,
                p.Title,
                p.Slug,
                p.Summary,
                p.Author!.DisplayName,
                p.PublishedAtUtc,
                p.IsPublished))
            .ToListAsync(cancellationToken);

        return (posts, totalCount);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PostListItemDto>> GetAllPostsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Posts
            .AsNoTracking()
            .OrderByDescending(p => p.UpdatedAtUtc)
            .Include(p => p.Author)
            .Select(p => new PostListItemDto(
                p.Id,
                p.Title,
                p.Slug,
                p.Summary,
                p.Author!.DisplayName,
                p.PublishedAtUtc,
                p.IsPublished))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PostDetailDto?> GetBySlugAsync(
        string slug, CancellationToken cancellationToken = default)
    {
        return await _context.Posts
            .AsNoTracking()
            .Where(p => p.Slug == slug)
            .Include(p => p.Author)
            .Select(p => new PostDetailDto(
                p.Id,
                p.Title,
                p.Slug,
                p.Content,
                p.Summary,
                p.Author!.DisplayName,
                p.CreatedAtUtc,
                p.UpdatedAtUtc,
                p.PublishedAtUtc,
                p.IsPublished))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Post?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Posts
            .Include(p => p.Author)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Post> CreateAsync(Post post, CancellationToken cancellationToken = default)
    {
        _context.Posts.Add(post);
        await _context.SaveChangesAsync(cancellationToken);
        return post;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Post post, CancellationToken cancellationToken = default)
    {
        _context.Posts.Update(post);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var post = await _context.Posts.FindAsync([id], cancellationToken);
        if (post is not null)
        {
            _context.Posts.Remove(post);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Posts.CountAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PostListItemDto>> GetRecentPostsAsync(
        int count, CancellationToken cancellationToken = default)
    {
        return await _context.Posts
            .AsNoTracking()
            .OrderByDescending(p => p.UpdatedAtUtc)
            .Take(count)
            .Include(p => p.Author)
            .Select(p => new PostListItemDto(
                p.Id,
                p.Title,
                p.Slug,
                p.Summary,
                p.Author!.DisplayName,
                p.PublishedAtUtc,
                p.IsPublished))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> IsSlugTakenAsync(string slug, Guid? excludePostId = null, CancellationToken cancellationToken = default)
    {
        if (excludePostId.HasValue)
        {
            return await _context.Posts
                .AnyAsync(p => p.Slug == slug && p.Id != excludePostId.Value, cancellationToken);
        }
        
        return await _context.Posts
            .AnyAsync(p => p.Slug == slug, cancellationToken);
    }
}
