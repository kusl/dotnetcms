using MyBlog.Core.Interfaces;
using MyBlog.Core.Services;
using Xunit;

namespace MyBlog.Tests.Unit;

/// <summary>
/// Mock implementation that always returns null to test no-dimension scenarios.
/// </summary>
internal sealed class NullImageDimensionService : IImageDimensionService
{
    public Task<(int Width, int Height)?> GetDimensionsAsync(string url, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<(int, int)?>(null);
    }
}

/// <summary>
/// Additional edge case tests for the MarkdownService.
/// Tests complex scenarios not covered by the main MarkdownServiceTests.
/// </summary>
public class MarkdownServiceEdgeCaseTests
{
    private readonly MarkdownService _sut = new(new NullImageDimensionService());

    // Helper to normalize newlines for cross-platform comparison
    private static string NormalizeNewlines(string s) => s.Replace("\r\n", "\n");

    [Fact]
    public async Task ToHtml_WithNestedBoldAndItalic_ProcessesBothCorrectly()
    {
        var result = await _sut.ToHtmlAsync("This is **bold with *nested italic* inside**");

        Assert.Contains("<strong>bold with <em>nested italic</em> inside</strong>", result);
    }

    [Fact]
    public async Task ToHtml_WithCodeBlockContainingSpecialCharacters_PreservesContent()
    {
        var markdown = "```\n<script>alert('xss')</script>\n```";
        var result = await _sut.ToHtmlAsync(markdown);

        Assert.Contains("<pre><code>", result);
        Assert.Contains("&lt;script&gt;", result);
        Assert.DoesNotContain("<script>", result);
    }

    [Fact]
    public async Task ToHtml_WithHtmlInParagraph_EscapesHtml()
    {
        var result = await _sut.ToHtmlAsync("<div>Not allowed</div>");

        Assert.Contains("&lt;div&gt;", result);
        Assert.DoesNotContain("<div>", result);
    }

    [Fact]
    public async Task ToHtml_WithMixedListTypes_ClosesListsProperly()
    {
        var markdown = "- Unordered item\n\n1. Ordered item";
        var result = NormalizeNewlines(await _sut.ToHtmlAsync(markdown));

        Assert.Contains("</ul>", result);
        Assert.Contains("<ol>", result);
    }

    [Fact]
    public async Task ToHtml_WithMultipleCodeBlocks_HandlesEachCorrectly()
    {
        var markdown = "```\nfirst block\n```\n\nSome text\n\n```\nsecond block\n```";
        var result = await _sut.ToHtmlAsync(markdown);

        var codeBlockCount = result.Split("<pre><code>").Length - 1;
        Assert.Equal(2, codeBlockCount);
    }

    [Fact]
    public async Task ToHtml_WithUnclosedCodeBlock_ClosesAutomatically()
    {
        var markdown = "```\nunclosed code block";
        var result = await _sut.ToHtmlAsync(markdown);

        Assert.Contains("</code></pre>", result);
    }

    [Fact]
    public async Task ToHtml_WithLinkContainingSpecialCharacters_EncodesCorrectly()
    {
        var result = await _sut.ToHtmlAsync("[Link](https://example.com/path?a=1&b=2)");

        Assert.Contains("href=\"https://example.com/path?a=1&amp;b=2\"", result);
    }

    [Fact]
    public async Task ToHtml_WithConsecutiveHeadings_ProcessesAll()
    {
        var markdown = "# H1\n## H2\n### H3";
        var result = await _sut.ToHtmlAsync(markdown);

        Assert.Contains("<h1>H1</h1>", result);
        Assert.Contains("<h2>H2</h2>", result);
        Assert.Contains("<h3>H3</h3>", result);
    }

    [Fact]
    public async Task ToHtml_WithMultipleBlockquotes_ProcessesEach()
    {
        var markdown = "> Quote 1\n\n> Quote 2";
        var result = await _sut.ToHtmlAsync(markdown);

        var quoteCount = result.Split("<blockquote>").Length - 1;
        Assert.Equal(2, quoteCount);
    }

    [Fact]
    public async Task ToHtml_WithHorizontalRuleVariations_AllWork()
    {
        var markdown1 = await _sut.ToHtmlAsync("---");
        var markdown2 = await _sut.ToHtmlAsync("***");
        var markdown3 = await _sut.ToHtmlAsync("___");
        var markdown4 = await _sut.ToHtmlAsync("----");

        Assert.Contains("<hr />", markdown1);
        Assert.Contains("<hr />", markdown2);
        Assert.Contains("<hr />", markdown3);
        Assert.Contains("<hr />", markdown4);
    }

    [Fact]
    public async Task ToHtml_WithUnorderedListVariations_BothWork()
    {
        var dashList = await _sut.ToHtmlAsync("- Item 1\n- Item 2");
        var asteriskList = await _sut.ToHtmlAsync("* Item 1\n* Item 2");

        Assert.Contains("<ul>", dashList);
        Assert.Contains("<ul>", asteriskList);
    }

    [Fact]
    public async Task ToHtml_WithLongOrderedList_MaintainsStructure()
    {
        var markdown = "1. First\n2. Second\n3. Third\n4. Fourth\n5. Fifth";
        var result = await _sut.ToHtmlAsync(markdown);

        var liCount = result.Split("<li>").Length - 1;
        Assert.Equal(5, liCount);
    }

    [Fact]
    public async Task ToHtml_WithInlineCodeInHeading_ProcessesBoth()
    {
        var result = await _sut.ToHtmlAsync("# Heading with `code`");

        Assert.Contains("<h1>Heading with <code>code</code></h1>", result);
    }

    [Fact]
    public async Task ToHtml_WithBoldInListItem_ProcessesBoth()
    {
        var result = await _sut.ToHtmlAsync("- Item with **bold**");

        Assert.Contains("<li>Item with <strong>bold</strong></li>", result);
    }

    [Fact]
    public async Task ToHtml_WithLinkInListItem_ProcessesBoth()
    {
        var result = await _sut.ToHtmlAsync("1. Check [this](https://example.com)");

        Assert.Contains("<li>Check <a href=\"https://example.com\">this</a></li>", result);
    }

    [Fact]
    public async Task ToHtml_WithEmptyLines_HandlesGracefully()
    {
        var markdown = "Paragraph 1\n\n\n\nParagraph 2";
        var result = await _sut.ToHtmlAsync(markdown);

        Assert.Contains("<p>Paragraph 1</p>", result);
        Assert.Contains("<p>Paragraph 2</p>", result);
    }

    [Fact]
    public async Task ToHtml_WithOnlyWhitespace_ReturnsEmpty()
    {
        var result = await _sut.ToHtmlAsync("   \n\n   \t   ");

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task ToHtml_WithImageAltTextContainingSpecialChars_EncodesAlt()
    {
        var result = await _sut.ToHtmlAsync("![Image with \"quotes\"](https://example.com/img.png)");

        Assert.Contains("alt=\"Image with &quot;quotes&quot;\"", result);
    }

    [Fact]
    public async Task ToHtml_WithEmojiInContent_PreservesEmoji()
    {
        var result = await _sut.ToHtmlAsync("Hello 👋 World 🌍");

        Assert.Contains("👋", result);
        Assert.Contains("🌍", result);
    }

    [Fact]
    public async Task ToHtml_WithVeryLongLine_ProcessesCorrectly()
    {
        var longText = new string('a', 10000);
        var result = await _sut.ToHtmlAsync(longText);

        Assert.Contains($"<p>{longText}</p>", result);
    }

    [Fact]
    public async Task ToHtml_WithHeadingAfterList_ClosesListFirst()
    {
        var markdown = "- Item 1\n- Item 2\n# Heading";
        var result = NormalizeNewlines(await _sut.ToHtmlAsync(markdown));

        // List should close before heading
        var ulCloseIndex = result.IndexOf("</ul>");
        var h1Index = result.IndexOf("<h1>");
        Assert.True(ulCloseIndex < h1Index);
    }

    [Fact]
    public async Task ToHtml_WithCodeBlockAfterList_ClosesListFirst()
    {
        var markdown = "- Item 1\n- Item 2\n```\ncode\n```";
        var result = NormalizeNewlines(await _sut.ToHtmlAsync(markdown));

        var ulCloseIndex = result.IndexOf("</ul>");
        var preIndex = result.IndexOf("<pre>");
        Assert.True(ulCloseIndex < preIndex);
    }
}
