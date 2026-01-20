using MyBlog.Core.Services;
using Xunit;

namespace MyBlog.Tests.Unit;

public class SlugServiceTests
{
    private readonly SlugService _sut = new();

    [Fact]
    public void GenerateSlug_WithSimpleTitle_ReturnsLowercaseWithHyphens()
    {
        var result = _sut.GenerateSlugOrUuid("Hello World");
        Assert.Equal("hello-world", result);
    }

    [Fact]
    public void GenerateSlug_WithSpecialCharacters_RemovesThem()
    {
        var result = _sut.GenerateSlugOrUuid("Hello, World! How's it going?");
        Assert.Equal("hello-world-hows-it-going", result);
    }

    [Fact]
    public void GenerateSlug_WithMultipleSpaces_CollapsesToSingleHyphen()
    {
        var result = _sut.GenerateSlugOrUuid("Hello    World");
        Assert.Equal("hello-world", result);
    }

    [Fact]
    public void GenerateSlug_WithUnicode_RemovesDiacritics()
    {
        var result = _sut.GenerateSlugOrUuid("Café résumé");
        Assert.Equal("cafe-resume", result);
    }

    [Fact]
    public void GenerateSlug_WithLeadingTrailingSpaces_TrimsHyphens()
    {
        var result = _sut.GenerateSlugOrUuid("  Hello World  ");
        Assert.Equal("hello-world", result);
    }

    [Fact]
    public void GenerateSlug_WithNumbers_PreservesNumbers()
    {
        var result = _sut.GenerateSlugOrUuid("Top 10 Tips for 2024");
        Assert.Equal("top-10-tips-for-2024", result);
    }

    [Fact]
    public void GenerateSlug_WithUnderscores_ConvertsToHyphens()
    {
        var result = _sut.GenerateSlugOrUuid("hello_world_test");
        Assert.Equal("hello-world-test", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("       ")]
    public void GenerateSlug_WithEmptyStringOrWhitespace_ReturnsGuidWithPrefix(string input)
    {
        // Act
        var result = _sut.GenerateSlugOrUuid(input);

        // Assert
        Assert.StartsWith("post-", result);

        // Extract the GUID part (the part after "post-")
        var guidPart = result.Replace("post-", "");

        // Assert it is a valid GUID
        var isValidGuid = Guid.TryParse(guidPart, out _);
        Assert.True(isValidGuid, $"Expected a valid GUID but got {guidPart}");
    }
}
