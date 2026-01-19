using System.Threading;
using System.Threading.Tasks;
using MyBlog.Core.Interfaces;
using MyBlog.Core.Services;
using Xunit;

namespace MyBlog.Tests.Unit;

// Mock implementation for testing
class MockImageDimensionService : IImageDimensionService
{
    public Task<(int Width, int Height)?> GetDimensionsAsync(string url, CancellationToken cancellationToken = default)
    {
        if (url.Contains("image.png"))
        {
            return Task.FromResult<(int, int)?>((100, 200));
        }

        return Task.FromResult<(int, int)?>(null);
    }
}

public class MarkdownServiceTests
{
    private readonly MarkdownService _sut = new(new MockImageDimensionService());

    [Fact]
    public async Task ToHtml_WithHeading1_ReturnsH1Tag()
    {
        var result = await _sut.ToHtmlAsync("# Hello");
        Assert.Contains("<h1>Hello</h1>", result);
    }

    [Fact]
    public async Task ToHtml_WithHeading2_ReturnsH2Tag()
    {
        var result = await _sut.ToHtmlAsync("## Hello");
        Assert.Contains("<h2>Hello</h2>", result);
    }

    [Fact]
    public async Task ToHtml_WithHeading6_ReturnsH6Tag()
    {
        var result = await _sut.ToHtmlAsync("###### Hello");
        Assert.Contains("<h6>Hello</h6>", result);
    }

    [Fact]
    public async Task ToHtml_WithBoldText_ReturnsStrongTag()
    {
        var result = await _sut.ToHtmlAsync("This is **bold** text");
        Assert.Contains("<strong>bold</strong>", result);
    }

    [Fact]
    public async Task ToHtml_WithItalicText_ReturnsEmTag()
    {
        var result = await _sut.ToHtmlAsync("This is *italic* text");
        Assert.Contains("<em>italic</em>", result);
    }

    [Fact]
    public async Task ToHtml_WithLink_ReturnsAnchorTag()
    {
        var result = await _sut.ToHtmlAsync("Check [this link](https://example.com)");
        Assert.Contains("<p>Check <a href=\"https://example.com\">this link</a></p>", result);
    }

    [Fact]
    public async Task ToHtml_WithImage_InjectsDimensions_IfResolvable()
    {
        // Mock returns 100x200 for 'image.png'
        var result = await _sut.ToHtmlAsync("![alt text](https://example.com/image.png)");
        Assert.Contains("<p><img src=\"https://example.com/image.png\" alt=\"alt text\" width=\"100\" height=\"200\" /></p>\n", result);
    }

    [Fact]
    public async Task ToHtml_WithImage_InjectsParagraphs_IfResolvable()
    {
        // Mock returns 100x200 for 'image.png'
        var result = await _sut.ToHtmlAsync("Check out this photo of when I was younger. ![high school graduation photo](https://example.com/image.png)");
        Assert.Contains("<p>Check out this photo of when I was younger. <img src=\"https://example.com/image.png\" alt=\"high school graduation photo\" width=\"100\" height=\"200\" /></p>\n", result);
    }

    [Fact]
    public async Task ToHtml_WithImage_NoDimensions_IfUnresolvable()
    {
        var result = await _sut.ToHtmlAsync("![alt text](https://example.com/unknown.jpg)");
        Assert.Contains("<p><img src=\"https://example.com/unknown.jpg\" alt=\"alt text\" /></p>\n", result);
    }

    [Fact]
    public async Task ToHtml_WithInlineCode_ReturnsCodeTag()
    {
        var result = await _sut.ToHtmlAsync("Use `code` here");
        Assert.Contains("<code>code</code>", result);
    }

    [Fact]
    public async Task ToHtml_WithCodeBlock_ReturnsPreCodeTags()
    {
        var markdown = "```\nvar x = 1;\n```";
        var result = await _sut.ToHtmlAsync(markdown);
        Assert.Contains("<pre><code>", result);
        Assert.Contains("var x = 1;", result);
        Assert.Contains("</code></pre>", result);
    }

    [Fact]
    public async Task ToHtml_WithBlockquote_ReturnsBlockquoteTag()
    {
        var result = await _sut.ToHtmlAsync("> This is a quote");
        Assert.Contains("<blockquote><p>This is a quote</p></blockquote>", result);
    }

    [Fact]
    public async Task ToHtml_WithUnorderedList_ReturnsUlLiTags()
    {
        var markdown = "- Item 1\n- Item 2";
        var result = await _sut.ToHtmlAsync(markdown);
        Assert.Contains("<ul>", result);
        Assert.Contains("<li>Item 1</li>", result);
        Assert.Contains("<li>Item 2</li>", result);
        Assert.Contains("</ul>", result);
    }

    [Fact]
    public async Task ToHtml_WithHorizontalRule_ReturnsHrTag()
    {
        var result = await _sut.ToHtmlAsync("---");
        Assert.Contains("<hr />", result);
    }

    [Fact]
    public async Task ToHtml_WithEmptyString_ReturnsEmptyString()
    {
        var result = await _sut.ToHtmlAsync("");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task ToHtml_WithPlainText_ReturnsParagraph()
    {
        var result = await _sut.ToHtmlAsync("Hello world");
        Assert.Contains("<p>Hello world</p>", result);
    }

    [Fact]
    public async Task ToHtml_WithHtmlCharacters_EscapesThem()
    {
        var result = await _sut.ToHtmlAsync("Use <script> tags");
        Assert.Contains("&lt;script&gt;", result);
        Assert.DoesNotContain("<script>", result);
    }
}
