using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using MyBlog.Core.Interfaces;

namespace MyBlog.Core.Services;

/// <summary>
/// Custom Markdown to HTML renderer.
/// Supports: headings, bold, italic, links, images, code blocks, blockquotes, lists, horizontal rules.
/// </summary>
public sealed partial class MarkdownService : IMarkdownService
{
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
        var inList = false;
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
                    if (inList)
                    {
                        result.AppendLine("</ul>");
                        inList = false;
                    }
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
                if (inList)
                {
                    result.AppendLine("</ul>");
                    inList = false;
                }
                result.AppendLine("<hr />");
                continue;
            }

            // Handle headings
            var headingMatch = HeadingPattern().Match(line);
            if (headingMatch.Success)
            {
                if (inList)
                {
                    result.AppendLine("</ul>");
                    inList = false;
                }
                var level = headingMatch.Groups[1].Value.Length;
                var text = ProcessInline(headingMatch.Groups[2].Value.Trim());
                result.AppendLine($"<h{level}>{text}</h{level}>");
                continue;
            }

            // Handle blockquotes
            if (line.StartsWith('>'))
            {
                if (inList)
                {
                    result.AppendLine("</ul>");
                    inList = false;
                }
                var quoteText = ProcessInline(line[1..].TrimStart());
                result.AppendLine($"<blockquote><p>{quoteText}</p></blockquote>");
                continue;
            }

            // Handle unordered lists
            var listMatch = UnorderedListPattern().Match(line);
            if (listMatch.Success)
            {
                if (!inList)
                {
                    result.AppendLine("<ul>");
                    inList = true;
                }
                var itemText = ProcessInline(listMatch.Groups[1].Value);
                result.AppendLine($"<li>{itemText}</li>");
                continue;
            }

            // Close list if no longer in list item
            if (inList && !string.IsNullOrWhiteSpace(line))
            {
                result.AppendLine("</ul>");
                inList = false;
            }

            // Handle empty lines
            if (string.IsNullOrWhiteSpace(line))
            {
                if (inList)
                {
                    result.AppendLine("</ul>");
                    inList = false;
                }
                continue;
            }

            // Regular paragraph
            var paragraphText = ProcessInline(line);
            result.AppendLine($"<p>{paragraphText}</p>");
        }

        // Close any open list
        if (inList)
        {
            result.AppendLine("</ul>");
        }

        // Close any unclosed code block
        if (inCodeBlock)
        {
            result.Append("<pre><code>");
            result.Append(HttpUtility.HtmlEncode(codeBlockContent.ToString().TrimEnd()));
            result.AppendLine("</code></pre>");
        }

        return result.ToString();
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
