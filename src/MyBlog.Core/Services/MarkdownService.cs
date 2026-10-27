using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MyBlog.Core.Interfaces;

namespace MyBlog.Core.Services;

public sealed partial class MarkdownService(
    IImageDimensionService imageDimensionService,
    ILogger<MarkdownService>? logger = null)
    : IMarkdownService
{
    private const string TokenStart = "\uE000";
    private const string TokenEnd = "\uE001";

    private enum ListType { None, Unordered, Ordered }

    private enum TableAlignment { None, Left, Center, Right }

    public async Task<string> ToHtmlAsync(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var result = new StringBuilder();
        var inCodeBlock = false;
        var currentListType = ListType.None;
        var codeBlockContent = new StringBuilder();

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];

            if (line.StartsWith("```"))
            {
                if (inCodeBlock)
                {
                    result.Append("<pre><code>");
                    result.Append(HtmlEncode(codeBlockContent.ToString().TrimEnd()));
                    result.AppendLine("</code></pre>");
                    codeBlockContent.Clear();
                    inCodeBlock = false;
                }
                else
                {
                    result.Append(CloseList(ref currentListType));
                    inCodeBlock = true;
                }
                continue;
            }

            if (inCodeBlock)
            {
                codeBlockContent.AppendLine(line);
                continue;
            }

            if (HorizontalRulePattern().IsMatch(line))
            {
                result.Append(CloseList(ref currentListType));
                result.AppendLine("<hr />");
                continue;
            }

            var headingMatch = HeadingPattern().Match(line);
            if (headingMatch.Success)
            {
                result.Append(CloseList(ref currentListType));
                var level = headingMatch.Groups[1].Value.Length;
                var text = await ProcessInlineAsync(headingMatch.Groups[2].Value);
                result.AppendLine($"<h{level}>{text}</h{level}>");
                continue;
            }

            if (line.StartsWith("> "))
            {
                result.Append(CloseList(ref currentListType));
                var quoteText = await ProcessInlineAsync(line[2..]);
                result.AppendLine($"<blockquote><p>{quoteText}</p></blockquote>");
                continue;
            }

            if (index + 1 < lines.Length && TryParseTableHeader(line, lines[index + 1], out var alignments))
            {
                result.Append(CloseList(ref currentListType));
                index = await AppendTableAsync(result, lines, index, alignments);
                continue;
            }

            var unorderedMatch = UnorderedListPattern().Match(line);
            if (unorderedMatch.Success)
            {
                if (currentListType != ListType.Unordered)
                {
                    result.Append(CloseList(ref currentListType));
                    result.AppendLine("<ul>");
                    currentListType = ListType.Unordered;
                }
                var itemText = await ProcessInlineAsync(unorderedMatch.Groups[1].Value);
                result.AppendLine($"<li>{itemText}</li>");
                continue;
            }

            var orderedMatch = OrderedListPattern().Match(line);
            if (orderedMatch.Success)
            {
                if (currentListType != ListType.Ordered)
                {
                    result.Append(CloseList(ref currentListType));
                    result.AppendLine(OpenOrderedList(orderedMatch.Groups[1].Value));
                    currentListType = ListType.Ordered;
                }

                var itemText = await ProcessInlineAsync(orderedMatch.Groups[2].Value);
                result.AppendLine($"<li>{itemText}</li>");
                continue;
            }

            if (currentListType != ListType.None && !string.IsNullOrWhiteSpace(line))
            {
                result.Append(CloseList(ref currentListType));
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                result.Append(CloseList(ref currentListType));
                continue;
            }

            var paragraphText = await ProcessInlineAsync(line);
            result.AppendLine($"<p>{paragraphText}</p>");
        }

        result.Append(CloseList(ref currentListType));

        if (inCodeBlock)
        {
            result.Append("<pre><code>");
            result.Append(HtmlEncode(codeBlockContent.ToString().TrimEnd()));
            result.AppendLine("</code></pre>");
        }

        return result.ToString();
    }

    private static string CloseList(ref ListType listType)
    {
        var result = listType switch
        {
            ListType.Unordered => "</ul>\n",
            ListType.Ordered => "</ol>\n",
            _ => ""
        };
        listType = ListType.None;
        return result;
    }

    private static string OpenOrderedList(string number)
    {
        return int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var start) && start != 1 ?
            $"<ol start=\"{start}\">" :
            "<ol>";
    }

    private static bool TryParseTableHeader(string headerLine, string delimiterLine, out TableAlignment[] alignments)
    {
        alignments = [];

        if (!headerLine.Contains('|') || !delimiterLine.Contains('|'))
        {
            return false;
        }

        var delimiterCells = SplitTableRow(delimiterLine);
        var parsed = new TableAlignment[delimiterCells.Count];
        for (var i = 0; i < delimiterCells.Count; i++)
        {
            var cell = delimiterCells[i];
            if (!TableDelimiterCellPattern().IsMatch(cell))
            {
                return false;
            }

            parsed[i] = (cell.StartsWith(':'), cell.EndsWith(':')) switch
            {
                (true, true) => TableAlignment.Center,
                (true, false) => TableAlignment.Left,
                (false, true) => TableAlignment.Right,
                _ => TableAlignment.None
            };
        }

        if (SplitTableRow(headerLine).Count != parsed.Length)
        {
            return false;
        }

        alignments = parsed;
        return true;
    }

    private async Task<int> AppendTableAsync(StringBuilder result, string[] lines, int headerIndex, TableAlignment[] alignments)
    {
        result.AppendLine("<div class=\"table-wrapper\"><table>");
        result.AppendLine("<thead>");
        await AppendTableRowAsync(result, lines[headerIndex], "th", alignments);
        result.AppendLine("</thead>");

        var index = headerIndex + 2;
        var hasBody = false;
        while (index < lines.Length && IsTableBodyRow(lines[index]))
        {
            if (!hasBody)
            {
                result.AppendLine("<tbody>");
                hasBody = true;
            }

            await AppendTableRowAsync(result, lines[index], "td", alignments);
            index++;
        }

        if (hasBody)
        {
            result.AppendLine("</tbody>");
        }

        result.AppendLine("</table></div>");
        return index - 1;
    }

    private async Task AppendTableRowAsync(StringBuilder result, string line, string cellTag, TableAlignment[] alignments)
    {
        var cells = SplitTableRow(line);
        result.Append("<tr>");
        for (var i = 0; i < alignments.Length; i++)
        {
            var content = i < cells.Count ? await ProcessInlineAsync(cells[i]) : string.Empty;
            var style = alignments[i] switch
            {
                TableAlignment.Left => " style=\"text-align: left\"",
                TableAlignment.Center => " style=\"text-align: center\"",
                TableAlignment.Right => " style=\"text-align: right\"",
                _ => ""
            };
            result.Append($"<{cellTag}{style}>{content}</{cellTag}>");
        }
        result.AppendLine("</tr>");
    }

    private static bool IsTableBodyRow(string line)
    {
        return !string.IsNullOrWhiteSpace(line)
            && !line.StartsWith("```")
            && !line.StartsWith("> ")
            && !HorizontalRulePattern().IsMatch(line)
            && !HeadingPattern().IsMatch(line)
            && !UnorderedListPattern().IsMatch(line)
            && !OrderedListPattern().IsMatch(line);
    }

    private static List<string> SplitTableRow(string line)
    {
        var row = line.Trim();
        if (row.StartsWith('|'))
        {
            row = row[1..];
        }

        if (row.EndsWith('|') && !row.EndsWith("\\|", StringComparison.Ordinal))
        {
            row = row[..^1];
        }

        var cells = new List<string>();
        var cell = new StringBuilder();
        var position = 0;
        while (position < row.Length)
        {
            var c = row[position];
            if (c == '\\' && position + 1 < row.Length && row[position + 1] == '|')
            {
                cell.Append('|');
                position += 2;
                continue;
            }

            if (c == '|')
            {
                cells.Add(cell.ToString().Trim());
                cell.Clear();
            }
            else
            {
                cell.Append(c);
            }

            position++;
        }

        cells.Add(cell.ToString().Trim());
        return cells;
    }

    private static string HtmlEncode(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            switch (c)
            {
                case '<':
                    sb.Append("&lt;");
                    break;
                case '>':
                    sb.Append("&gt;");
                    break;
                case '&':
                    sb.Append("&amp;");
                    break;
                case '"':
                    sb.Append("&quot;");
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    private static string Protect(List<string> protectedHtml, string html)
    {
        protectedHtml.Add(html);
        return $"{TokenStart}{protectedHtml.Count - 1}{TokenEnd}";
    }

    private async Task<string> ProcessInlineAsync(string text)
    {
        text = HtmlEncode(text)
            .Replace(TokenStart, "&#xE000;")
            .Replace(TokenEnd, "&#xE001;");

        var protectedHtml = new List<string>();

        text = InlineCodePattern().Replace(text, match => Protect(protectedHtml, $"<code>{match.Groups[1].Value}</code>"));

        var matches = ImagePattern().Matches(text);
        if (matches.Count > 0)
        {
            for (var i = matches.Count - 1; i >= 0; i--)
            {
                var match = matches[i];
                var alt = match.Groups[1].Value;
                var url = match.Groups[2].Value;

                string imgTag;
                try
                {
                    var dimensions = await imageDimensionService.GetDimensionsAsync(WebUtility.HtmlDecode(url));

                    imgTag = dimensions.HasValue ?
                        $"<img src=\"{url}\" alt=\"{alt}\" width=\"{dimensions.Value.Width}\" height=\"{dimensions.Value.Height}\" />" :
                        $"<img src=\"{url}\" alt=\"{alt}\" />";
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Failed to get dimensions for image {Url}. Rendering without dimensions.", url);
                    imgTag = $"<img src=\"{url}\" alt=\"{alt}\" />";
                }

                text = text.Remove(match.Index, match.Length).Insert(match.Index, Protect(protectedHtml, imgTag));
            }
        }

        text = LinkPattern().Replace(text, match => $"{Protect(protectedHtml, $"<a href=\"{match.Groups[2].Value}\">")}{match.Groups[1].Value}</a>");

        text = BoldPattern().Replace(text, match =>
        {
            var content = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            return $"<strong>{content}</strong>";
        });

        text = ItalicPattern().Replace(text, match =>
        {
            var content = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            return $"<em>{content}</em>";
        });

        return ProtectedTokenPattern().Replace(text, match => protectedHtml[int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)]);
    }

    [GeneratedRegex(@"^(#{1,6})\s+(.+)$")]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"^[-*]\s+(.+)$")]
    private static partial Regex UnorderedListPattern();

    [GeneratedRegex(@"^(\d+)\.\s+(.+)$")]
    private static partial Regex OrderedListPattern();

    [GeneratedRegex(@"^[-*_]{3,}\s*$")]
    private static partial Regex HorizontalRulePattern();

    [GeneratedRegex(@"^:?-+:?$")]
    private static partial Regex TableDelimiterCellPattern();

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex InlineCodePattern();

    [GeneratedRegex(@"!\[([^\]]*)\]\(([^)]+)\)")]
    private static partial Regex ImagePattern();

    [GeneratedRegex(@"\[([^\]]+)\]\(([^)]+)\)")]
    private static partial Regex LinkPattern();

    [GeneratedRegex(@"\*\*(.+?)\*\*|(?<![\p{L}\p{N}_])__(.+?)__(?![\p{L}\p{N}_])")]
    private static partial Regex BoldPattern();

    [GeneratedRegex(@"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)|(?<![\p{L}\p{N}_])_(?!_)(.+?)(?<!_)_(?![\p{L}\p{N}_])")]
    private static partial Regex ItalicPattern();

    [GeneratedRegex(@"\uE000(\d+)\uE001")]
    private static partial Regex ProtectedTokenPattern();
}
