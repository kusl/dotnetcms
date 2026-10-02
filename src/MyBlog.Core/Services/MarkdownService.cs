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
    private enum ListType { None, Unordered, Ordered }

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

        foreach (var rawLine in lines)
        {
            var line = rawLine;

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
                    result.AppendLine("<ol>");
                    currentListType = ListType.Ordered;
                }

                var itemText = await ProcessInlineAsync(orderedMatch.Groups[1].Value);
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

    private async Task<string> ProcessInlineAsync(string text)
    {
        text = HtmlEncode(text);

        text = InlineCodePattern().Replace(text, "<code>$1</code>");

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

                text = text.Remove(match.Index, match.Length).Insert(match.Index, imgTag);
            }
        }

        text = LinkPattern().Replace(text, "<a href=\"$2\">$1</a>");

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

        return text;
    }

    [GeneratedRegex(@"^(#{1,6})\s+(.+)$")]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"^[-*]\s+(.+)$")]
    private static partial Regex UnorderedListPattern();

    [GeneratedRegex(@"^\d+\.\s+(.+)$")]
    private static partial Regex OrderedListPattern();

    [GeneratedRegex(@"^[-*_]{3,}\s*$")]
    private static partial Regex HorizontalRulePattern();

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex InlineCodePattern();

    [GeneratedRegex(@"!\[([^\]]*)\]\(([^)]+)\)")]
    private static partial Regex ImagePattern();

    [GeneratedRegex(@"\[([^\]]+)\]\(([^)]+)\)")]
    private static partial Regex LinkPattern();

    [GeneratedRegex(@"\*\*(.+?)\*\*|__(.+?)__")]
    private static partial Regex BoldPattern();

    [GeneratedRegex(@"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)|(?<!_)_(?!_)(.+?)(?<!_)_(?!_)")]
    private static partial Regex ItalicPattern();
}
