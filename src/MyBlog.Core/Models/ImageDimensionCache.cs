namespace MyBlog.Core.Models;

/// <summary>
/// Caches dimensions for external images to prevent layout shifts.
/// </summary>
public sealed class ImageDimensionCache
{
    /// <summary>The absolute URL of the image (Primary Key).</summary>
    public required string Url { get; set; }

    /// <summary>The detected width of the image.</summary>
    public int Width { get; set; }

    /// <summary>The detected height of the image.</summary>
    public int Height { get; set; }

    /// <summary>When this record was last updated.</summary>
    public DateTime LastCheckedUtc { get; set; }
}
