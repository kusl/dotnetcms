using MyBlog.Core.Services;
using Xunit;

namespace MyBlog.Tests.Unit;

public class SlugServiceTests
{
    private readonly SlugService _sut = new();

    [Fact]
    public void GenerateSlug_WithSimpleTitle_ReturnsLowercaseWithHyphens()
    {
        var result = _sut.GenerateSlug("Hello World");
        Assert.Equal("hello-world", result);
    }

    [Fact]
    public void GenerateSlug_WithSpecialCharacters_RemovesThem()
    {
        var result = _sut.GenerateSlug("Hello, World! How's it going?");
        Assert.Equal("hello-world-hows-it-going", result);
    }

    [Fact]
    public void GenerateSlug_WithMultipleSpaces_CollapsesToSingleHyphen()
    {
        var result = _sut.GenerateSlug("Hello    World");
        Assert.Equal("hello-world", result);
    }

    [Fact]
    public void GenerateSlug_WithUnicode_RemovesDiacritics()
    {
        var result = _sut.GenerateSlug("Café résumé");
        Assert.Equal("cafe-resume", result);
    }

    [Fact]
    public void GenerateSlug_WithLeadingTrailingSpaces_TrimsHyphens()
    {
        var result = _sut.GenerateSlug("  Hello World  ");
        Assert.Equal("hello-world", result);
    }

    [Fact]
    public void GenerateSlug_WithNumbers_PreservesNumbers()
    {
        var result = _sut.GenerateSlug("Top 10 Tips for 2024");
        Assert.Equal("top-10-tips-for-2024", result);
    }

    [Fact]
    public void GenerateSlug_WithUnderscores_ConvertsToHyphens()
    {
        var result = _sut.GenerateSlug("hello_world_test");
        Assert.Equal("hello-world-test", result);
    }

    [Fact]
    public void GenerateSlug_WithEmptyString_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _sut.GenerateSlug(""));
    }

    [Fact]
    public void GenerateSlug_WithWhitespaceOnly_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _sut.GenerateSlug("   "));
    }
}
