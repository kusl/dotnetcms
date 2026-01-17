namespace MyBlog.Core.Interfaces;

/// <summary>
/// Service for resolving and caching the width and height of images from URLs.
/// </summary>
public interface IImageDimensionService
{
    /// <summary>
    /// Gets the dimensions (width, height) of an image from a URL.
    /// Checks the database cache first; fetches and caches if missing.
    /// </summary>
    /// <param name="url">The absolute URL of the image.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing Width and Height, or null if resolution fails.</returns>
    Task<(int Width, int Height)?> GetDimensionsAsync(string url, CancellationToken cancellationToken = default);
}
