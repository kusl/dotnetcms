using MyBlog.Core.Services;
using Xunit;

namespace MyBlog.Tests.Unit;

public class MarkdownServiceBlockTests
{
    private readonly MarkdownService _sut = new(new MockImageDimensionService());

    [Fact]
    public async Task ToHtml_WithConsecutiveQuoteLines_RendersOneBlockquote()
    {
        var result = await _sut.ToHtmlAsync("> First line\n> Second line\n>\n>Third");

        Assert.Equal(1, result.Split("<blockquote>").Length - 1);
        Assert.Contains("<blockquote><p>First line</p>\n<p>Second line</p>\n<p>Third</p></blockquote>", result);
    }

    [Fact]
    public async Task ToHtml_WithBlocksInsideQuote_RendersNestedBlocks()
    {
        var result = await _sut.ToHtmlAsync("> ## Note\n> - one\n> - two\n> > inner");

        Assert.Contains("<blockquote><h2>Note</h2>\n<ul>\n<li>one</li>\n<li>two</li>\n</ul>\n<blockquote><p>inner</p></blockquote></blockquote>", result);
    }

    [Fact]
    public async Task ToHtml_WithCodeFenceInsideQuote_PreservesCodeLines()
    {
        var result = await _sut.ToHtmlAsync("> ```\n> a\n> b\n> ```");

        Assert.Contains("<blockquote><pre><code>a\nb</code></pre></blockquote>", result);
    }

    [Fact]
    public async Task ToHtml_WithQuoteAfterList_ClosesList()
    {
        var result = await _sut.ToHtmlAsync("- item\n> quote");

        Assert.Contains("</ul>\n<blockquote><p>quote</p></blockquote>", result);
    }

    [Fact]
    public async Task ToHtml_WithTableFollowedByQuote_EndsTable()
    {
        var result = await _sut.ToHtmlAsync("| a |\n| - |\n| 1 |\n>quote");

        Assert.Contains("</table></div>\n<blockquote><p>quote</p></blockquote>", result);
    }

    [Fact]
    public async Task ToHtml_WithClosingHashesInHeading_StripsThem()
    {
        var result = await _sut.ToHtmlAsync("## Title ##\n### C#\n# Spaces   ");

        Assert.Contains("<h2>Title</h2>", result);
        Assert.Contains("<h3>C#</h3>", result);
        Assert.Contains("<h1>Spaces</h1>", result);
    }

    [Fact]
    public async Task ToHtml_WithSpacedHorizontalRules_RendersRules()
    {
        var result = await _sut.ToHtmlAsync("* * *\n\n- - -\n\n_ _ _");

        Assert.Equal(3, result.Split("<hr />").Length - 1);
        Assert.DoesNotContain("<li>", result);
    }

    [Fact]
    public async Task ToHtml_WithMixedRuleCharacters_DoesNotRenderRule()
    {
        var result = await _sut.ToHtmlAsync("-*-");

        Assert.DoesNotContain("<hr />", result);
    }

    [Fact]
    public async Task ToHtml_WithPlusBullets_RendersUnorderedList()
    {
        var result = await _sut.ToHtmlAsync("+ one\n+ two");

        Assert.Contains("<ul>\n<li>one</li>\n<li>two</li>\n</ul>", result);
    }
}
