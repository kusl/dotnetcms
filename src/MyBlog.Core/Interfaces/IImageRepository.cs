using MyBlog.Core.Models;

namespace MyBlog.Core.Interfaces;

/// <summary>
/// Repository interface for image operations.
/// </summary>
public interface IImageRepository
{
    /// <summary>Gets all images.</summary>
    Task<IReadOnlyList<Image>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets an image by ID.</summary>
    Task<Image?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates a new image.</summary>
    Task<Image> CreateAsync(Image image, CancellationToken cancellationToken = default);

    /// <summary>Deletes an image by ID.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
