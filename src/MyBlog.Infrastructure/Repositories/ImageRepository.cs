using Microsoft.EntityFrameworkCore;
using MyBlog.Core.Interfaces;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;

namespace MyBlog.Infrastructure.Repositories;

public sealed class ImageRepository : IImageRepository
{
    private readonly BlogDbContext _context;

    public ImageRepository(BlogDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Image>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _context.Images
            .AsNoTracking()
            .OrderByDescending(i => i.UploadedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<Image?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => await _context.Images.FindAsync([id], cancellationToken);

    public async Task<Image> CreateAsync(Image image, CancellationToken cancellationToken = default)
    {
        _context.Images.Add(image);
        await _context.SaveChangesAsync(cancellationToken);
        return image;
    }

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
