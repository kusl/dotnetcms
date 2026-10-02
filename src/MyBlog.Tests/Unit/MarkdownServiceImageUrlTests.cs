using MyBlog.Core.Interfaces;
using MyBlog.Core.Services;
using Xunit;

namespace MyBlog.Tests.Unit;

public class MarkdownServiceImageUrlTests
{
    [Fact]
    public async Task ToHtml_WithImageQueryString_LooksUpRawUrlAndRendersEncodedSrc()
    {
        var recorder = new RecordingImageDimensionService();
        var sut = new MarkdownService(recorder);

        var result = await sut.ToHtmlAsync("![alt](https://example.com/img.png?w=1&h=2)");

        var url = Assert.Single(recorder.Urls);
        Assert.Equal("https://example.com/img.png?w=1&h=2", url);
        Assert.Contains("<img src=\"https://example.com/img.png?w=1&amp;h=2\" alt=\"alt\" width=\"10\" height=\"20\" />", result);
    }

    private sealed class RecordingImageDimensionService : IImageDimensionService
    {
        public List<string> Urls { get; } = [];

        public Task<(int Width, int Height)?> GetDimensionsAsync(string url, CancellationToken cancellationToken = default)
        {
            Urls.Add(url);
            return Task.FromResult<(int, int)?>((10, 20));
        }
    }
}
