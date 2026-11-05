using MyBlog.Core.Interfaces;
using MyBlog.Core.Services;
using Xunit;

namespace MyBlog.Tests.Unit;

public class MarkdownServiceLinkTests
{
    private readonly MarkdownService _sut = new(new MockImageDimensionService());

    [Fact]
    public async Task ToHtml_WithParenthesesInLinkUrl_KeepsWholeUrl()
    {
        var result = await _sut.ToHtmlAsync("- **The president still signs or vetoes.** [Congress can override a veto](https://en.wikipedia.org/wiki/Reconciliation_(United_States_Congress)) only with two-thirds of both chambers.");

        Assert.Contains("<li><strong>The president still signs or vetoes.</strong> <a href=\"https://en.wikipedia.org/wiki/Reconciliation_(United_States_Congress)\">Congress can override a veto</a> only with two-thirds of both chambers.</li>", result);
    }

    [Fact]
    public async Task ToHtml_WithNestedParenthesesInLinkUrl_KeepsWholeUrl()
    {
        var result = await _sut.ToHtmlAsync("[x](https://example.com/a_(b_(c))_d) after");

        Assert.Contains("<a href=\"https://example.com/a_(b_(c))_d\">x</a> after", result);
    }

    [Fact]
    public async Task ToHtml_WithPercentEncodedParentheses_KeepsUrl()
    {
        var result = await _sut.ToHtmlAsync("[x](https://en.wikipedia.org/wiki/Reconciliation_%28United_States_Congress%29)");

        Assert.Contains("<a href=\"https://en.wikipedia.org/wiki/Reconciliation_%28United_States_Congress%29\">x</a>", result);
    }

    [Fact]
    public async Task ToHtml_WithLinkInsideParentheses_DoesNotSwallowClosingParenthesis()
    {
        var result = await _sut.ToHtmlAsync("(see [docs](https://example.com/docs))");

        Assert.Contains("<p>(see <a href=\"https://example.com/docs\">docs</a>)</p>", result);
    }

    [Fact]
    public async Task ToHtml_WithUnbalancedParenthesisInLinkUrl_RendersLiteralText()
    {
        var result = await _sut.ToHtmlAsync("[x](https://example.com/a(b)");

        Assert.DoesNotContain("<a ", result);
    }

    [Fact]
    public async Task ToHtml_WithEscapedParenthesisInLinkUrl_KeepsWholeUrl()
    {
        var result = await _sut.ToHtmlAsync("[x](https://example.com/a\\)b)");

        Assert.Contains("<a href=\"https://example.com/a)b\">x</a>", result);
    }

    [Fact]
    public async Task ToHtml_WithAngleBracketLinkDestination_AllowsSpacesAndParentheses()
    {
        var result = await _sut.ToHtmlAsync("[x](<https://example.com/a b)c>)");

        Assert.Contains("<a href=\"https://example.com/a b)c\">x</a>", result);
    }

    [Fact]
    public async Task ToHtml_WithLinkTitle_RendersTitleAttribute()
    {
        var result = await _sut.ToHtmlAsync("[x](https://example.com/a_(b) \"The *title*\") and [y](https://example.com 'Single')");

        Assert.Contains("<a href=\"https://example.com/a_(b)\" title=\"The *title*\">x</a>", result);
        Assert.Contains("<a href=\"https://example.com\" title=\"Single\">y</a>", result);
    }

    [Fact]
    public async Task ToHtml_WithBracketsInLinkText_KeepsWholeText()
    {
        var result = await _sut.ToHtmlAsync("[see [1] here](https://example.com)");

        Assert.Contains("<a href=\"https://example.com\">see [1] here</a>", result);
    }

    [Fact]
    public async Task ToHtml_WithSpaceInBareLinkUrl_RendersLiteralText()
    {
        var result = await _sut.ToHtmlAsync("[x](not a url)");

        Assert.Contains("<p>[x](not a url)</p>", result);
    }

    [Fact]
    public async Task ToHtml_WithParenthesesInImageUrl_KeepsWholeUrl()
    {
        var result = await _sut.ToHtmlAsync("![chart](https://example.com/image.png?v=(2)) done");

        Assert.Contains("<img src=\"https://example.com/image.png?v=(2)\" alt=\"chart\" width=\"100\" height=\"200\" /> done", result);
    }

    [Fact]
    public async Task ToHtml_WithImageTitle_RendersTitleAttribute()
    {
        var result = await _sut.ToHtmlAsync("![alt](https://example.com/unknown.jpg \"Caption\")");

        Assert.Contains("<img src=\"https://example.com/unknown.jpg\" alt=\"alt\" title=\"Caption\" />", result);
    }

    [Fact]
    public async Task ToHtml_WithInlineCodeInImageAlt_RendersPlainAlt()
    {
        var result = await _sut.ToHtmlAsync("![run `make`](https://example.com/unknown.jpg)");

        Assert.Contains("<img src=\"https://example.com/unknown.jpg\" alt=\"run make\" />", result);
        Assert.DoesNotContain("\uE000", result);
    }

    [Fact]
    public async Task ToHtml_WithJavaScriptLink_RendersTextOnly()
    {
        var result = await _sut.ToHtmlAsync("[click](javascript:alert(1)) and [b](JaVaScRiPt:alert(2)) and [c](<  javascript:alert(3)>)");

        Assert.DoesNotContain("<a ", result);
        Assert.DoesNotContain("href", result);
        Assert.Contains("click", result);
    }

    [Fact]
    public async Task ToHtml_WithEscapedColonInJavaScriptLink_RendersTextOnly()
    {
        var result = await _sut.ToHtmlAsync("[click](javascript\\:alert(1))");

        Assert.DoesNotContain("<a ", result);
    }

    [Fact]
    public async Task ToHtml_WithDataImage_RendersAltOnly()
    {
        var result = await _sut.ToHtmlAsync("![pic](data:text/html,x)");

        Assert.DoesNotContain("<img", result);
        Assert.Contains("<p>pic</p>", result);
    }

    [Fact]
    public async Task ToHtml_WithSafeSchemesAndRelativeUrls_RendersLinks()
    {
        var result = await _sut.ToHtmlAsync("[a](mailto:me@example.com) [b](/post/x) [c](#top) [d](HTTPS://example.com) [e](?q=a:b)");

        Assert.Contains("<a href=\"mailto:me@example.com\">a</a>", result);
        Assert.Contains("<a href=\"/post/x\">b</a>", result);
        Assert.Contains("<a href=\"#top\">c</a>", result);
        Assert.Contains("<a href=\"HTTPS://example.com\">d</a>", result);
        Assert.Contains("<a href=\"?q=a:b\">e</a>", result);
    }

    [Fact]
    public async Task ToHtml_WithAutolink_RendersAnchorWithoutEmphasis()
    {
        var result = await _sut.ToHtmlAsync("See <https://example.com/a_b_c?x=1&y=2> or <me@example.com>.");

        Assert.Contains("<a href=\"https://example.com/a_b_c?x=1&amp;y=2\">https://example.com/a_b_c?x=1&amp;y=2</a>", result);
        Assert.Contains("<a href=\"mailto:me@example.com\">me@example.com</a>", result);
        Assert.DoesNotContain("<em>", result);
    }

    [Fact]
    public async Task ToHtml_WithJavaScriptAutolink_RendersLiteralText()
    {
        var result = await _sut.ToHtmlAsync("<javascript:alert(1)>");

        Assert.DoesNotContain("<a ", result);
        Assert.Contains("&lt;javascript:alert(1)&gt;", result);
    }

    [Fact]
    public async Task ToHtml_WithBackslashEscapes_RendersLiteralCharacters()
    {
        var result = await _sut.ToHtmlAsync("\\*not italic\\* \\_nor this\\_ \\[not a link\\](x) \\`tick\\` \\\\ \\<b\\>");

        Assert.Contains("<p>*not italic* _nor this_ [not a link](x) `tick` \\ &lt;b&gt;</p>", result);
    }

    [Fact]
    public async Task ToHtml_WithBackslashInInlineCode_KeepsBackslash()
    {
        var result = await _sut.ToHtmlAsync("Use `C:\\temp\\*` and C:\\Users");

        Assert.Contains("<code>C:\\temp\\*</code> and C:\\Users", result);
    }

    [Fact]
    public void GetImageUrls_ReturnsDecodedUrlsOutsideCode()
    {
        var markdown = "![a](https://example.com/a_(1).png \"t\")\n```\n![b](https://example.com/b.png)\n```\nText `![c](https://example.com/c.png)` and ![d](https://example.com/d.png?x=1&y=2) ![e](javascript:alert(1))";

        var urls = _sut.GetImageUrls(markdown);

        Assert.Equal(new[] { "https://example.com/a_(1).png", "https://example.com/d.png?x=1&y=2" }, urls);
    }

    [Fact]
    public void GetImageUrls_WithEmptyMarkdown_ReturnsEmpty()
    {
        Assert.Empty(_sut.GetImageUrls(""));
    }

    [Fact]
    public async Task ToHtml_WithImageUrlFromGetImageUrls_LooksUpSameUrl()
    {
        var recorder = new RecordingDimensionService();
        var sut = new MarkdownService(recorder);
        var markdown = "![a](https://example.com/a_(1).png?x=1&y=2 \"t\")";

        await sut.ToHtmlAsync(markdown);

        Assert.Equal(sut.GetImageUrls(markdown), recorder.Urls);
    }

    private sealed class RecordingDimensionService : IImageDimensionService
    {
        public List<string> Urls { get; } = [];

        public Task<(int Width, int Height)?> GetDimensionsAsync(string url, CancellationToken cancellationToken = default)
        {
            Urls.Add(url);
            return Task.FromResult<(int, int)?>(null);
        }
    }
}
