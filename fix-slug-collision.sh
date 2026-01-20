#!/bin/bash
set -euo pipefail

# =============================================================================
# Fix Slug Collision Issue
# =============================================================================
# 1. Adds IsSlugTakenAsync to IPostRepository/PostRepository
# 2. Updates PostEditor.razor to handle duplicate slugs by appending numbers
# =============================================================================

echo "Applying Slug Uniqueness Fix..."

# 1. Update IPostRepository
# -----------------------------------------------------------------------------
echo "Updating IPostRepository.cs..."
cat << 'EOF' > src/MyBlog.Core/Interfaces/IPostRepository.cs
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

    /// <summary>Checks if a slug is already in use by another post.</summary>
    Task<bool> IsSlugTakenAsync(string slug, Guid? excludePostId = null, CancellationToken cancellationToken = default);
}
EOF

# 2. Update PostRepository Implementation
# -----------------------------------------------------------------------------
echo "Updating PostRepository.cs..."
cat << 'EOF' > src/MyBlog.Infrastructure/Repositories/PostRepository.cs
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
EOF

# 3. Update PostEditor to use uniqueness logic
# -----------------------------------------------------------------------------
echo "Updating PostEditor.razor..."
cat << 'EOF' > src/MyBlog.Web/Components/Pages/Admin/PostEditor.razor
@page "/admin/posts/new"
@page "/admin/posts/edit/{Id:guid}"
@rendermode InteractiveServer
@attribute [Authorize]
@inject IPostRepository PostRepository
@inject ISlugService SlugService
@inject NavigationManager Navigation
@inject AuthenticationStateProvider AuthStateProvider
@using System.Security.Claims

<PageTitle>@(_isEdit ? "Edit Post" : "New Post")</PageTitle>

<h1>@(_isEdit ? "Edit Post" : "New Post")</h1>

@if (_loading)
{
    <p>Loading...</p>
}
else if (_isEdit && _existingPost is null)
{
    <p>Post not found.</p>
    <a href="/admin/posts">Back to Posts</a>
}
else
{
    <div class="post-editor">
        <div class="editor-form">
            <div class="form-group">
                <label for="title">Title</label>
                <input type="text" id="title" @bind="_title" @bind:event="oninput" />
            </div>

            <div class="form-group">
                <label for="summary">Summary</label>
                <textarea id="summary" @bind="_summary" rows="2"></textarea>
            </div>

            <div class="form-group">
                <label for="content">Content (Markdown)</label>
                <textarea id="content" @bind="_content" @bind:event="oninput" rows="20"></textarea>
            </div>

            <div class="form-group checkbox">
                <label>
                    <input type="checkbox" @bind="_isPublished" />
                    Published
                </label>
            </div>

            @if (!string.IsNullOrEmpty(_error))
            {
                <div class="error-message">@_error</div>
            }

            <div class="form-actions">
                <button @onclick="Save" class="btn btn-primary" disabled="@_saving">
                    @(_saving ? "Saving..." : "Save")
                </button>
                <a href="/admin/posts" class="btn">Cancel</a>
            </div>
        </div>

        <div class="editor-preview">
            <h3>Preview</h3>
            <div class="preview-content">
                <MarkdownRenderer Content="@_content" />
            </div>
        </div>
    </div>
}

@code {
    [Parameter]
    public Guid? Id { get; set; }

    private bool _isEdit => Id.HasValue;
    private bool _loading = true;
    private bool _saving;
    private string _title = "";
    private string _summary = "";
    private string _content = "";
    private bool _isPublished;
    private string? _error;
    private Post? _existingPost;

    protected override async Task OnInitializedAsync()
    {
        if (_isEdit)
        {
            _existingPost = await PostRepository.GetByIdAsync(Id!.Value);
            if (_existingPost is not null)
            {
                _title = _existingPost.Title;
                _summary = _existingPost.Summary;
                _content = _existingPost.Content;
                _isPublished = _existingPost.IsPublished;
            }
        }
        _loading = false;
    }

    private async Task Save()
    {
        if (string.IsNullOrWhiteSpace(_title))
        {
            _error = "Title is required.";
            return;
        }

        _saving = true;
        _error = null;

        try
        {
            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            var userIdClaim = authState.User.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                _error = "Unable to identify current user. Please log in again.";
                _saving = false;
                return;
            }

            // Generate unique slug
            var baseSlug = SlugService.GenerateSlugOrUuid(_title);
            var finalSlug = baseSlug;
            var counter = 1;

            // Loop until we find a slug that isn't taken (excluding current post if editing)
            while (await PostRepository.IsSlugTakenAsync(finalSlug, _isEdit ? Id : null))
            {
                finalSlug = $"{baseSlug}-{counter}";
                counter++;
            }

            if (_isEdit && _existingPost is not null)
            {
                _existingPost.Title = _title;
                _existingPost.Slug = finalSlug; // Update slug with unique version
                _existingPost.Summary = _summary;
                _existingPost.Content = _content;
                _existingPost.IsPublished = _isPublished;
                _existingPost.UpdatedAtUtc = DateTime.UtcNow;
                if (_isPublished && !_existingPost.PublishedAtUtc.HasValue)
                {
                    _existingPost.PublishedAtUtc = DateTime.UtcNow;
                }

                await PostRepository.UpdateAsync(_existingPost);
            }
            else
            {
                var post = new Post
                {
                    Id = Guid.NewGuid(),
                    Title = _title,
                    Slug = finalSlug, // Use unique slug
                    Summary = _summary,
                    Content = _content,
                    AuthorId = userId,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow,
                    IsPublished = _isPublished,
                    PublishedAtUtc = _isPublished ? DateTime.UtcNow : null
                };
                await PostRepository.CreateAsync(post);
            }

            Navigation.NavigateTo("/admin/posts");
        }
        catch (Exception ex)
        {
            _error = $"Failed to save: {ex.Message}";
        }
        finally
        {
            _saving = false;
        }
    }
}
EOF

echo "Done. Slug collisions are now handled automatically."
