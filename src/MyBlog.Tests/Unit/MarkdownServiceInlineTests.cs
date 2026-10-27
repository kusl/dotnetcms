using MyBlog.Core.Services;
using Xunit;

namespace MyBlog.Tests.Unit;

public class MarkdownServiceInlineTests
{
    private readonly MarkdownService _sut = new(new MockImageDimensionService());

    [Fact]
    public async Task ToHtml_WithUnderscoresInLinkUrl_PreservesUrl()
    {
        var result = await _sut.ToHtmlAsync("[page](https://example.com/mp_/http://example.org/applicant_s2s/page.jsp)");

        Assert.Contains("<a href=\"https://example.com/mp_/http://example.org/applicant_s2s/page.jsp\">page</a>", result);
        Assert.DoesNotContain("<em>", result);
    }

    [Fact]
    public async Task ToHtml_WithAsterisksInLinkUrl_PreservesUrl()
    {
        var result = await _sut.ToHtmlAsync("[a](https://example.com/*x*) and [b](https://example.com/*y*)");

        Assert.Contains("href=\"https://example.com/*x*\"", result);
        Assert.Contains("href=\"https://example.com/*y*\"", result);
        Assert.DoesNotContain("<em>", result);
    }

    [Fact]
    public async Task ToHtml_WithEmphasisInLinkText_ProcessesEmphasis()
    {
        var result = await _sut.ToHtmlAsync("[**bold** link](https://example.com)");

        Assert.Contains("<a href=\"https://example.com\"><strong>bold</strong> link</a>", result);
    }

    [Fact]
    public async Task ToHtml_WithEmphasisMarkersInInlineCode_LeavesCodeLiteral()
    {
        var result = await _sut.ToHtmlAsync("Call `__init__` and `*args*` here");

        Assert.Contains("<code>__init__</code>", result);
        Assert.Contains("<code>*args*</code>", result);
        Assert.DoesNotContain("<strong>", result);
        Assert.DoesNotContain("<em>", result);
    }

    [Fact]
    public async Task ToHtml_WithMarkdownInsideInlineCode_LeavesCodeLiteral()
    {
        var result = await _sut.ToHtmlAsync("Write `[text](url)` or `![alt](img.png)`");

        Assert.Contains("<code>[text](url)</code>", result);
        Assert.Contains("<code>![alt](img.png)</code>", result);
        Assert.DoesNotContain("<a ", result);
        Assert.DoesNotContain("<img ", result);
    }

    [Fact]
    public async Task ToHtml_WithEmphasisMarkersInImageAlt_LeavesAltLiteral()
    {
        var result = await _sut.ToHtmlAsync("![a *b* c](https://example.com/unknown.jpg)");

        Assert.Contains("<img src=\"https://example.com/unknown.jpg\" alt=\"a *b* c\" />", result);
    }

    [Fact]
    public async Task ToHtml_WithLinkedImage_RendersImageInsideAnchor()
    {
        var result = await _sut.ToHtmlAsync("[![alt](https://example.com/unknown.jpg)](https://example.com)");

        Assert.Contains("<a href=\"https://example.com\"><img src=\"https://example.com/unknown.jpg\" alt=\"alt\" /></a>", result);
    }

    [Fact]
    public async Task ToHtml_WithIntrawordUnderscores_DoesNotEmphasize()
    {
        var result = await _sut.ToHtmlAsync("Use snake_case_names and __dunder__method here");

        Assert.Contains("snake_case_names", result);
        Assert.DoesNotContain("<em>", result);
        Assert.DoesNotContain("<strong>", result);
    }

    [Fact]
    public async Task ToHtml_WithUnderscoreEmphasis_StillEmphasizes()
    {
        var result = await _sut.ToHtmlAsync("This is _italic_ and __bold__ text.");

        Assert.Contains("<em>italic</em>", result);
        Assert.Contains("<strong>bold</strong>", result);
    }

    [Fact]
    public async Task ToHtml_WithPrivateUseCharacters_PreservesThemEncoded()
    {
        var result = await _sut.ToHtmlAsync("a\uE0000\uE001b");

        Assert.Contains("<p>a&#xE000;0&#xE001;b</p>", result);
    }

    [Fact]
    public async Task ToHtml_WithOrderedListNotStartingAtOne_SetsStart()
    {
        var result = await _sut.ToHtmlAsync("5. Fifth\n6. Sixth");

        Assert.Contains("<ol start=\"5\">", result);
        Assert.Contains("<li>Fifth</li>", result);
        Assert.Contains("<li>Sixth</li>", result);
    }

    [Fact]
    public async Task ToHtml_WithOrderedListStartingAtOne_OmitsStart()
    {
        var result = await _sut.ToHtmlAsync("1. First\n2. Second");

        Assert.Contains("<ol>", result);
        Assert.DoesNotContain("start=", result);
    }

    [Fact]
    public async Task ToHtml_WithOrderedListContinuedAfterParagraph_KeepsNumbering()
    {
        var markdown = "1. One\n2. Two\n\n**Award**\n\n3. Three";
        var result = await _sut.ToHtmlAsync(markdown);

        Assert.Contains("<p><strong>Award</strong></p>", result);
        Assert.Contains("<ol start=\"3\">", result);
    }
}
