using System.Text;
using MyBlog.Core.Services;
using Xunit;

namespace MyBlog.Tests.Unit;

public class MarkdownServiceLongDocumentTests
{
    private readonly MarkdownService _sut = new(new MockImageDimensionService());

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

        Assert.DoesNotContain("*", result);
    }

    [Fact]
    public async Task ToHtml_WithRealWorldArticle_EncodesQuotesAndPreservesEmDash()
    {
        var result = await _sut.ToHtmlAsync(BuildArticle());

        Assert.Contains("&quot;carefully conducted&quot;", result);
        Assert.Contains("\u2014", result);

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
