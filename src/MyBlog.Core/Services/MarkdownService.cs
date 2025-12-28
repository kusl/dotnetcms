using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using MyBlog.Core.Interfaces;

namespace MyBlog.Core.Services;

/// <summary>
/// Custom Markdown to HTML renderer.
/// Supports: headings, bold, italic, links, images, code blocks, blockquotes, 
/// unordered lists, ordered lists, horizontal rules.
/// </summary>
public sealed partial class MarkdownService : IMarkdownService
{
    private enum ListType { None, Unordered, Ordered }

    /// <inheritdoc />
    public string ToHtml(string markdown)
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

            // Handle fenced code blocks
            if (line.StartsWith("```"))
            {
                if (inCodeBlock)
                {
                    result.Append("<pre><code>");
                    result.Append(HttpUtility.HtmlEncode(codeBlockContent.ToString().TrimEnd()));
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

            // Handle horizontal rules
            if (HorizontalRulePattern().IsMatch(line))
            {
                result.Append(CloseList(ref currentListType));
                result.AppendLine("<hr />");
                continue;
            }

            // Handle headings
            var headingMatch = HeadingPattern().Match(line);
            if (headingMatch.Success)
            {
                result.Append(CloseList(ref currentListType));
                var level = headingMatch.Groups[1].Value.Length;
                var headingText = ProcessInline(headingMatch.Groups[2].Value);
                result.AppendLine($"<h{level}>{headingText}</h{level}>");
                continue;
            }

            // Handle blockquotes
            if (line.StartsWith("> "))
            {
                result.Append(CloseList(ref currentListType));
                var quoteText = ProcessInline(line[2..]);
                result.AppendLine($"<blockquote><p>{quoteText}</p></blockquote>");
                continue;
            }

            // Handle unordered list items (- or *)
            var unorderedMatch = UnorderedListPattern().Match(line);
            if (unorderedMatch.Success)
            {
                if (currentListType != ListType.Unordered)
                {
                    result.Append(CloseList(ref currentListType));
                    result.AppendLine("<ul>");
                    currentListType = ListType.Unordered;
                }
                var itemText = ProcessInline(unorderedMatch.Groups[1].Value);
                result.AppendLine($"<li>{itemText}</li>");
                continue;
            }

            // Handle ordered list items (1. 2. 3. etc.)
            var orderedMatch = OrderedListPattern().Match(line);
            if (orderedMatch.Success)
            {
                if (currentListType != ListType.Ordered)
                {
                    result.Append(CloseList(ref currentListType));
                    result.AppendLine("<ol>");
                    currentListType = ListType.Ordered;
                }
                var itemText = ProcessInline(orderedMatch.Groups[1].Value);
                result.AppendLine($"<li>{itemText}</li>");
                continue;
            }

            // Close list if no longer in list item
            if (currentListType != ListType.None && !string.IsNullOrWhiteSpace(line))
            {
                result.Append(CloseList(ref currentListType));
            }

            // Handle empty lines
            if (string.IsNullOrWhiteSpace(line))
            {
                result.Append(CloseList(ref currentListType));
                continue;
            }

            // Regular paragraph
            var paragraphText = ProcessInline(line);
            result.AppendLine($"<p>{paragraphText}</p>");
        }

        // Close any open list
        result.Append(CloseList(ref currentListType));

        // Close any unclosed code block
        if (inCodeBlock)
        {
            result.Append("<pre><code>");
            result.Append(HttpUtility.HtmlEncode(codeBlockContent.ToString().TrimEnd()));
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

    private static string ProcessInline(string text)
    {
        // Escape HTML first
        text = HttpUtility.HtmlEncode(text);

        // Process inline code (must be before bold/italic to avoid conflicts)
        text = InlineCodePattern().Replace(text, "<code>$1</code>");

        // Process images ![alt](url)
        text = ImagePattern().Replace(text, "<img src=\"$2\" alt=\"$1\" />");

        // Process links [text](url)
        text = LinkPattern().Replace(text, "<a href=\"$2\">$1</a>");

        // Process bold **text** or __text__
        text = BoldPattern().Replace(text, "<strong>$1</strong>");

        // Process italic *text* or _text_
        text = ItalicPattern().Replace(text, "<em>$1</em>");

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

    [GeneratedRegex(@"\*\*([^*]+)\*\*|__([^_]+)__")]
    private static partial Regex BoldPattern();

    [GeneratedRegex(@"(?<!\*)\*(?!\*)([^*]+)(?<!\*)\*(?!\*)|(?<!_)_(?!_)([^_]+)(?<!_)_(?!_)")]
    private static partial Regex ItalicPattern();
}
