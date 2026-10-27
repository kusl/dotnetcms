using MyBlog.Core.Services;
using Xunit;

namespace MyBlog.Tests.Unit;

public class MarkdownServiceTableTests
{
    private readonly MarkdownService _sut = new(new MockImageDimensionService());

    private static string NormalizeNewlines(string s) => s.Replace("\r\n", "\n");

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
    public async Task ToHtml_WithSimpleTable_RendersHeaderAndBody()
    {
        var markdown = "| Name | Value |\n|---|---|\n| a | 1 |\n| b | 2 |";
        var result = NormalizeNewlines(await _sut.ToHtmlAsync(markdown));

        Assert.Equal(
            "<div class=\"table-wrapper\"><table>\n" +
            "<thead>\n" +
            "<tr><th>Name</th><th>Value</th></tr>\n" +
            "</thead>\n" +
            "<tbody>\n" +
            "<tr><td>a</td><td>1</td></tr>\n" +
            "<tr><td>b</td><td>2</td></tr>\n" +
            "</tbody>\n" +
            "</table></div>\n",
            result);
    }

    [Fact]
    public async Task ToHtml_WithTableWithoutOuterPipes_RendersTable()
    {
        var markdown = "Name | Value\n--- | ---\na | 1";
        var result = await _sut.ToHtmlAsync(markdown);

        Assert.Contains("<tr><th>Name</th><th>Value</th></tr>", result);
        Assert.Contains("<tr><td>a</td><td>1</td></tr>", result);
        Assert.DoesNotContain("<p>", result);
    }

    [Fact]
    public async Task ToHtml_WithAlignedColumns_AppliesTextAlign()
    {
        var markdown = "| L | C | R | N |\n|:--|:-:|--:|---|\n| 1 | 2 | 3 | 4 |";
        var result = await _sut.ToHtmlAsync(markdown);

        Assert.Contains("<th style=\"text-align: left\">L</th>", result);
        Assert.Contains("<th style=\"text-align: center\">C</th>", result);
        Assert.Contains("<th style=\"text-align: right\">R</th>", result);
        Assert.Contains("<th>N</th>", result);
        Assert.Contains("<td style=\"text-align: right\">3</td>", result);
    }

    [Fact]
    public async Task ToHtml_WithDelimiterSpacing_RendersTable()
    {
        var markdown = "| A | B |\n| :--- | ---: |\n| x | y |";
        var result = await _sut.ToHtmlAsync(markdown);

        Assert.Contains("<th style=\"text-align: left\">A</th>", result);
        Assert.Contains("<td style=\"text-align: right\">y</td>", result);
    }

    [Fact]
    public async Task ToHtml_WithInlineMarkdownInCells_ProcessesInline()
    {
        var markdown = "| Term | Source |\n|---|---|\n| **Bold** and *italic* | [Docs](https://example.com/a_b_c) and `code` |";
        var result = await _sut.ToHtmlAsync(markdown);

        Assert.Contains("<td><strong>Bold</strong> and <em>italic</em></td>", result);
        Assert.Contains("<td><a href=\"https://example.com/a_b_c\">Docs</a> and <code>code</code></td>", result);
    }

    [Fact]
    public async Task ToHtml_WithEscapedPipe_KeepsPipeInCell()
    {
        var markdown = "| Expression | Meaning |\n|---|---|\n| a \\| b | either |";
        var result = await _sut.ToHtmlAsync(markdown);

        Assert.Contains("<tr><td>a | b</td><td>either</td></tr>", result);
    }

    [Fact]
    public async Task ToHtml_WithShortAndLongRows_NormalizesToHeaderColumnCount()
    {
        var markdown = "| A | B | C |\n|---|---|---|\n| 1 |\n| 1 | 2 | 3 | 4 |";
        var result = await _sut.ToHtmlAsync(markdown);

        Assert.Contains("<tr><td>1</td><td></td><td></td></tr>", result);
        Assert.Contains("<tr><td>1</td><td>2</td><td>3</td></tr>", result);
        Assert.DoesNotContain("4", result);
    }

    [Fact]
    public async Task ToHtml_WithHeaderOnly_OmitsBody()
    {
        var markdown = "| A | B |\n|---|---|";
        var result = await _sut.ToHtmlAsync(markdown);

        Assert.Contains("<thead>", result);
        Assert.DoesNotContain("<tbody>", result);
        Assert.Contains("</table></div>", result);
    }

    [Fact]
    public async Task ToHtml_WithMismatchedDelimiterCount_DoesNotRenderTable()
    {
        var markdown = "| A | B |\n|---|\n| 1 | 2 |";
        var result = await _sut.ToHtmlAsync(markdown);

        Assert.DoesNotContain("<table>", result);
        Assert.Contains("<p>| A | B |</p>", result);
    }

    [Fact]
    public async Task ToHtml_WithPipeLineFollowedByHorizontalRule_DoesNotRenderTable()
    {
        var markdown = "a | b\n---";
        var result = await _sut.ToHtmlAsync(markdown);

        Assert.DoesNotContain("<table>", result);
        Assert.Contains("<p>a | b</p>", result);
        Assert.Contains("<hr />", result);
    }

    [Fact]
    public async Task ToHtml_WithTableFollowedByBlankLineAndParagraph_EndsTable()
    {
        var markdown = "| A |\n|---|\n| 1 |\n\nAfter the table.";
        var result = NormalizeNewlines(await _sut.ToHtmlAsync(markdown));

        Assert.Contains("</table></div>\n<p>After the table.</p>", result);
        Assert.Equal(1, CountOccurrences(result, "<td>"));
    }

    [Fact]
    public async Task ToHtml_WithTableFollowedByHeading_EndsTable()
    {
        var markdown = "| A |\n|---|\n| 1 |\n## Next";
        var result = NormalizeNewlines(await _sut.ToHtmlAsync(markdown));

        Assert.Contains("</table></div>\n<h2>Next</h2>", result);
    }

    [Fact]
    public async Task ToHtml_WithTableFollowedByList_EndsTable()
    {
        var markdown = "| A |\n|---|\n| 1 |\n- item";
        var result = NormalizeNewlines(await _sut.ToHtmlAsync(markdown));

        Assert.Contains("</table></div>\n<ul>\n<li>item</li>\n</ul>", result);
    }

    [Fact]
    public async Task ToHtml_WithListBeforeTable_ClosesListFirst()
    {
        var markdown = "- item\n| A |\n|---|\n| 1 |";
        var result = NormalizeNewlines(await _sut.ToHtmlAsync(markdown));

        var ulCloseIndex = result.IndexOf("</ul>", StringComparison.Ordinal);
        var tableIndex = result.IndexOf("<table>", StringComparison.Ordinal);
        Assert.True(ulCloseIndex >= 0 && ulCloseIndex < tableIndex);
    }

    [Fact]
    public async Task ToHtml_WithHtmlInCells_EscapesHtml()
    {
        var markdown = "| A |\n|---|\n| <script>alert(1)</script> |";
        var result = await _sut.ToHtmlAsync(markdown);

        Assert.Contains("<td>&lt;script&gt;alert(1)&lt;/script&gt;</td>", result);
        Assert.DoesNotContain("<script>", result);
    }

    [Fact]
    public async Task ToHtml_WithTableInsideCodeBlock_RendersAsCode()
    {
        var markdown = "```\n| A | B |\n|---|---|\n```";
        var result = await _sut.ToHtmlAsync(markdown);

        Assert.DoesNotContain("<table>", result);
        Assert.Contains("| A | B |", result);
    }

    [Fact]
    public async Task ToHtml_WithMultipleTablesOfDifferentSizes_RendersEach()
    {
        var markdown = "| A | B |\n|---|---|\n| 1 | 2 |\n\n| C | D | E | F |\n|---|---|---|---|\n| 1 | 2 | 3 | 4 |\n| 5 | 6 | 7 | 8 |";
        var result = await _sut.ToHtmlAsync(markdown);

        Assert.Equal(2, CountOccurrences(result, "<table>"));
        Assert.Equal(6, CountOccurrences(result, "<th>"));
        Assert.Equal(10, CountOccurrences(result, "<td>"));
    }

    [Fact]
    public async Task ToHtml_WithImageInCell_InjectsDimensions()
    {
        var markdown = "| Image |\n|---|\n| ![alt](https://example.com/image.png) |";
        var result = await _sut.ToHtmlAsync(markdown);

        Assert.Contains("<td><img src=\"https://example.com/image.png\" alt=\"alt\" width=\"100\" height=\"200\" /></td>", result);
    }

    [Fact]
    public async Task ToHtml_WithCrlfTable_RendersTable()
    {
        var markdown = "| A | B |\r\n|---|---|\r\n| 1 | 2 |";
        var result = await _sut.ToHtmlAsync(markdown);

        Assert.Contains("<tr><td>1</td><td>2</td></tr>", result);
    }

    [Fact]
    public async Task ToHtml_WithRealWorldArticle_RendersTablesListsAndLinks()
    {
        var lines = new[]
        {
            "## 2. The building blocks (glossary)",
            "",
            "| Term | Plain-language meaning | Source |",
            "|---|---|---|",
            "| **Grant / cooperative agreement** | The two kinds of assistance opportunities Grants.gov lists. | [Grants 101](https://www.grants.gov/learn-grants/grants-101.html) |",
            "| **Solicitation** | An informal synonym, also called a \"solicitation.\" In *procurement* (contracts), it differs. | [NIH glossary](https://www.ninds.nih.gov/glossary?title=NOFO), [NSF PAPPG 2022](https://www.nsf.gov/pubs/policydocs/pappg22_1/pappg_1.jsp) |",
            "| **AOR (Authorized Organization Representative)** | The person who can actually submit. | [NIH](https://grants.nih.gov/grants/tracking_application.htm) |",
            "",
            "**Pre-award**",
            "1. The agency posts the opportunity.",
            "2. Applicants search for it.",
            "",
            "**Award**",
            "5. Federal staff make recommendations.",
            "",
            "- **[Inference]** Older docs used hostnames like `apply07` ([archived testing page](https://cybercemetery.unt.edu/oilspill/20130213205425mp_/http://grants.gov/applicant_s2s/applicant-testing.jsp)).",
            "",
            "| Agency / office | What public sources show | Source |",
            "|---|---|---|",
            "| **NIH** | Opportunities are listed in Grants.gov. | [NIH tracking](https://grants.nih.gov/grants/tracking_application.htm) |"
        };

        var result = await _sut.ToHtmlAsync(string.Join("\n", lines));

        Assert.Equal(2, CountOccurrences(result, "<table>"));
        Assert.Equal(2, CountOccurrences(result, "<div class=\"table-wrapper\">"));
        Assert.Equal(6, CountOccurrences(result, "<tr>"));
        Assert.Contains("<th>Plain-language meaning</th>", result);
        Assert.Contains("<td><strong>Solicitation</strong></td>", result);
        Assert.Contains("&quot;solicitation.&quot; In <em>procurement</em> (contracts), it differs.", result);
        Assert.Contains("<a href=\"https://www.nsf.gov/pubs/policydocs/pappg22_1/pappg_1.jsp\">NSF PAPPG 2022</a>", result);
        Assert.Contains("<a href=\"https://cybercemetery.unt.edu/oilspill/20130213205425mp_/http://grants.gov/applicant_s2s/applicant-testing.jsp\">archived testing page</a>", result);
        Assert.Contains("<code>apply07</code>", result);
        Assert.Contains("<ol start=\"5\">", result);
        Assert.DoesNotContain("|", result);
        Assert.Equal(CountOccurrences(result, "<em>"), CountOccurrences(result, "</em>"));
        Assert.Equal(CountOccurrences(result, "<strong>"), CountOccurrences(result, "</strong>"));
    }
}
