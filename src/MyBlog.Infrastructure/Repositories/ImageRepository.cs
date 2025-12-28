using Microsoft.EntityFrameworkCore;
using MyBlog.Core.Interfaces;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;

namespace MyBlog.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of the image repository.
/// </summary>
public sealed class ImageRepository : IImageRepository
{
    private readonly BlogDbContext _context;

    /// <summary>Initializes a new instance of ImageRepository.</summary>
    public ImageRepository(BlogDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Image>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Images
            .AsNoTracking()
            .OrderByDescending(i => i.UploadedAtUtc)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Image?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Images.FindAsync([id], cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Image> CreateAsync(Image image, CancellationToken cancellationToken = default)
    {
        _context.Images.Add(image);
        await _context.SaveChangesAsync(cancellationToken);
        return image;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var image = await _context.Images.FindAsync([id], cancellationToken);
        if (image is not null)
        {
            _context.Images.Remove(image);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
