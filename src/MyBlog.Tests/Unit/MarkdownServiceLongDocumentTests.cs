using System.Text;
using MyBlog.Core.Services;
using Xunit;

namespace MyBlog.Tests.Unit;

/// <summary>
/// Regression tests for long, real-world article bodies.
///
/// These exist because a full-length post silently failed to save through the
/// admin editor. The parser turned out to be fine; the failure was in the editor's
/// data binding and the 32 KB default SignalR receive limit. These tests pin down
/// the parser side of that boundary so a future change cannot quietly break it.
/// </summary>
public class MarkdownServiceLongDocumentTests
{
    private readonly MarkdownService _sut = new(new MockImageDimensionService());

    /// <summary>
    /// Builds a document shaped like a real published article: an H1, a lede
    /// paragraph, a horizontal rule, section headings, inline emphasis, straight
    /// double quotes, em dashes and a trailing sourcing note.
    /// </summary>
    private static string BuildArticle()
    {
        var lines = new[]
        {
            "# Kids' Lungs Recovered Faster Than Anyone Expected",
            "",
            "A five-year London study found children's lung growth caught up \u2014 and the reason it worked so fast matters.",
            "",
            "---",
            "",
            "## The study",
            "",
            "The findings were published in *The Lancet Public Health*.",
            "",
            "An expert said \"carefully conducted\" and moved on.",
            "",
            "### Sourcing note",
            "",
            "Core findings are drawn from the university press release."
        };

        return string.Join("\n", lines);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = haystack.IndexOf(needle, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = haystack.IndexOf(needle, index + needle.Length, StringComparison.Ordinal);
        }
        return count;
    }

    [Fact]
    public async Task ToHtml_WithRealWorldArticle_ProducesExpectedBlockStructure()
    {
        var result = await _sut.ToHtmlAsync(BuildArticle());

        Assert.Contains("<h1>Kids' Lungs Recovered Faster Than Anyone Expected</h1>", result);
        Assert.Contains("<h2>The study</h2>", result);
        Assert.Contains("<h3>Sourcing note</h3>", result);
        Assert.Contains("<hr />", result);
        Assert.Equal(4, CountOccurrences(result, "<p>"));
        Assert.Equal(4, CountOccurrences(result, "</p>"));
    }

    [Fact]
    public async Task ToHtml_WithRealWorldArticle_ProducesBalancedInlineEmphasis()
    {
        var result = await _sut.ToHtmlAsync(BuildArticle());

        Assert.Contains("<em>The Lancet Public Health</em>", result);
        Assert.Equal(
            CountOccurrences(result, "<em>"),
            CountOccurrences(result, "</em>"));

        // Every asterisk should have been consumed by the emphasis pass.
        Assert.DoesNotContain("*", result);
    }

    [Fact]
    public async Task ToHtml_WithRealWorldArticle_EncodesQuotesAndPreservesEmDash()
    {
        var result = await _sut.ToHtmlAsync(BuildArticle());

        // Double quotes are escaped; Unicode punctuation is passed through untouched.
        Assert.Contains("&quot;carefully conducted&quot;", result);
        Assert.Contains("\u2014", result);

        // Apostrophes are intentionally not escaped by HtmlEncode.
        Assert.Contains("children's", result);
    }

    [Fact]
    public async Task ToHtml_WithDocumentLargerThanDefaultSignalRLimit_ParsesEveryBlock()
    {
        const int sectionCount = 250;
        const string paragraph =
            "Air pollution concentration falls off sharply with distance from its source, and the " +
            "gradient shows up at scales most people never think about, right down to a few meters " +
            "of sidewalk on one side of a street rather than the other.";

        var builder = new StringBuilder();
        for (var i = 1; i <= sectionCount; i++)
        {
            builder.Append("## Section ").Append(i).Append('\n');
            builder.Append('\n');
            builder.Append(paragraph).Append('\n');
            builder.Append('\n');
        }
        builder.Append("The final paragraph must survive intact.");

        var markdown = builder.ToString();

        // Sanity check: this input is deliberately larger than SignalR's 32 KB default.
        Assert.True(
            markdown.Length > 32 * 1024,
            $"Test input should exceed the 32 KB default message size, but was {markdown.Length} characters.");

        var result = await _sut.ToHtmlAsync(markdown);

        Assert.Equal(sectionCount, CountOccurrences(result, "<h2>"));
        Assert.Equal(sectionCount, CountOccurrences(result, "</h2>"));
        Assert.Equal(sectionCount + 1, CountOccurrences(result, "<p>"));
        Assert.Contains("<h2>Section 1</h2>", result);
        Assert.Contains($"<h2>Section {sectionCount}</h2>", result);
        Assert.Contains("<p>The final paragraph must survive intact.</p>", result);
    }
}
