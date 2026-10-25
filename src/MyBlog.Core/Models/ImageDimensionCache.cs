namespace MyBlog.Core.Models;

public sealed class ImageDimensionCache
{
    public required string Url { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public DateTime LastCheckedUtc { get; set; }
}
