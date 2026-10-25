namespace MyBlog.Core.Interfaces;

public interface IImageDimensionService
{
    Task<(int Width, int Height)?> GetDimensionsAsync(string url, CancellationToken cancellationToken = default);
}
