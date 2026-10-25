using MyBlog.Core.Models;

namespace MyBlog.Core.Interfaces;

public interface IImageRepository
{
    Task<IReadOnlyList<Image>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Image?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Image> CreateAsync(Image image, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
