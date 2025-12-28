using MyBlog.Core.Services;
using Xunit;

namespace MyBlog.Tests.Unit;

public class MarkdownServiceTests
{
    private readonly MarkdownService _sut = new();

    [Fact]
    public void ToHtml_WithHeading1_ReturnsH1Tag()
    {
        var result = _sut.ToHtml("# Hello");
        Assert.Contains("<h1>Hello</h1>", result);
    }

    [Fact]
    public void ToHtml_WithHeading2_ReturnsH2Tag()
    {
        var result = _sut.ToHtml("## Hello");
        Assert.Contains("<h2>Hello</h2>", result);
    }

    [Fact]
    public void ToHtml_WithHeading6_ReturnsH6Tag()
    {
        var result = _sut.ToHtml("###### Hello");
        Assert.Contains("<h6>Hello</h6>", result);
    }

    [Fact]
    public void ToHtml_WithBoldText_ReturnsStrongTag()
    {
        var result = _sut.ToHtml("This is **bold** text");
        Assert.Contains("<strong>bold</strong>", result);
    }

    [Fact]
    public void ToHtml_WithItalicText_ReturnsEmTag()
    {
        var result = _sut.ToHtml("This is *italic* text");
        Assert.Contains("<em>italic</em>", result);
    }

    [Fact]
    public void ToHtml_WithLink_ReturnsAnchorTag()
    {
        var result = _sut.ToHtml("Check [this link](https://example.com)");
        Assert.Contains("<a href=\"https://example.com\">this link</a>", result);
    }

    [Fact]
    public void ToHtml_WithImage_ReturnsImgTag()
    {
        var result = _sut.ToHtml("![alt text](https://example.com/image.png)");
        Assert.Contains("<img src=\"https://example.com/image.png\" alt=\"alt text\" />", result);
    }

    [Fact]
    public void ToHtml_WithInlineCode_ReturnsCodeTag()
    {
        var result = _sut.ToHtml("Use `code` here");
        Assert.Contains("<code>code</code>", result);
    }

    [Fact]
    public void ToHtml_WithCodeBlock_ReturnsPreCodeTags()
    {
        var markdown = "```\nvar x = 1;\n```";
        var result = _sut.ToHtml(markdown);
        Assert.Contains("<pre><code>", result);
        Assert.Contains("var x = 1;", result);
        Assert.Contains("</code></pre>", result);
    }

    [Fact]
    public void ToHtml_WithBlockquote_ReturnsBlockquoteTag()
    {
        var result = _sut.ToHtml("> This is a quote");
        Assert.Contains("<blockquote><p>This is a quote</p></blockquote>", result);
    }

    [Fact]
    public void ToHtml_WithUnorderedList_ReturnsUlLiTags()
    {
        var markdown = "- Item 1\n- Item 2";
        var result = _sut.ToHtml(markdown);
        Assert.Contains("<ul>", result);
        Assert.Contains("<li>Item 1</li>", result);
        Assert.Contains("<li>Item 2</li>", result);
        Assert.Contains("</ul>", result);
    }

    [Fact]
    public void ToHtml_WithHorizontalRule_ReturnsHrTag()
    {
        var result = _sut.ToHtml("---");
        Assert.Contains("<hr />", result);
    }

    [Fact]
    public void ToHtml_WithEmptyString_ReturnsEmptyString()
    {
        var result = _sut.ToHtml("");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ToHtml_WithPlainText_ReturnsParagraph()
    {
        var result = _sut.ToHtml("Hello world");
        Assert.Contains("<p>Hello world</p>", result);
    }

    [Fact]
    public void ToHtml_WithHtmlCharacters_EscapesThem()
    {
        var result = _sut.ToHtml("Use <script> tags");
        Assert.Contains("&lt;script&gt;", result);
        Assert.DoesNotContain("<script>", result);
    }
}
