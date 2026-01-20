using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Extensions.Logging;
using MyBlog.Core.Interfaces;

namespace MyBlog.Core.Services;

/// <summary>
/// Custom Markdown parser that converts Markdown text to HTML.
/// Supports: headings, bold, italic, links, images, code blocks, blockquotes, lists, horizontal rules.
/// </summary>
public sealed partial class MarkdownService : IMarkdownService
{
    private readonly IImageDimensionService _imageDimensionService;
    private readonly ILogger<MarkdownService>? _logger;

    public MarkdownService(IImageDimensionService imageDimensionService, ILogger<MarkdownService>? logger = null)
    {
        _imageDimensionService = imageDimensionService;
        _logger = logger;
    }

    private enum ListType { None, Unordered, Ordered }

    /// <inheritdoc />
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
                var text = await ProcessInlineAsync(headingMatch.Groups[2].Value);
                result.AppendLine($"<h{level}>{text}</h{level}>");
                continue;
            }

            // Handle blockquotes
            if (line.StartsWith("> "))
            {
                result.Append(CloseList(ref currentListType));
                var quoteText = await ProcessInlineAsync(line[2..]);
                result.AppendLine($"<blockquote><p>{quoteText}</p></blockquote>");
                continue;
            }

            // Handle unordered lists
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

            // Handle ordered lists
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
            var paragraphText = await ProcessInlineAsync(line);
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

    private async Task<string> ProcessInlineAsync(string text)
    {
        // Escape HTML first
        text = HttpUtility.HtmlEncode(text);

        // Process inline code
        text = InlineCodePattern().Replace(text, "<code>$1</code>");

        // Process images ![alt](url) -> WITH AUTOMATED DIMENSION LOOKUP
        // Regex replacement doesn't support async, so we find matches, process them, and replace.
        var matches = ImagePattern().Matches(text);
        if (matches.Count > 0)
        {
            // Process matches in reverse to avoid index drift
            for (int i = matches.Count - 1; i >= 0; i--)
            {
                var match = matches[i];
                var alt = match.Groups[1].Value;
                var url = match.Groups[2].Value;

                string imgTag;
                try
                {
                    // Lookup dimensions (Fast DB check or background fetch)
                    // This is wrapped in try-catch to ensure we never fail rendering
                    var dimensions = await _imageDimensionService.GetDimensionsAsync(url);

                    if (dimensions.HasValue)
                    {
                        imgTag = $"<img src=\"{url}\" alt=\"{alt}\" width=\"{dimensions.Value.Width}\" height=\"{dimensions.Value.Height}\" />";
                    }
                    else
                    {
                        // No dimensions available - render without width/height
                        imgTag = $"<img src=\"{url}\" alt=\"{alt}\" />";
                    }
                }
                catch (Exception ex)
                {
                    // If dimension lookup fails for any reason, still render the image
                    _logger?.LogWarning(ex, "Failed to get dimensions for image {Url}. Rendering without dimensions.", url);
                    imgTag = $"<img src=\"{url}\" alt=\"{alt}\" />";
                }

                // Replace the Markdown syntax with the HTML tag
                text = text.Remove(match.Index, match.Length).Insert(match.Index, imgTag);
            }
        }

        // Process links
        text = LinkPattern().Replace(text, "<a href=\"$2\">$1</a>");

        // Process bold
        text = BoldPattern().Replace(text, "<strong>$1</strong>");

        // Process italic
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

    // Standard markdown image pattern: ![alt](url)
    [GeneratedRegex(@"!\[([^\]]*)\]\(([^)]+)\)")]
    private static partial Regex ImagePattern();

    [GeneratedRegex(@"\[([^\]]+)\]\(([^)]+)\)")]
    private static partial Regex LinkPattern();

    [GeneratedRegex(@"\*\*([^*]+)\*\*|__([^_]+)__")]
    private static partial Regex BoldPattern();

    [GeneratedRegex(@"(?<!\*)\*(?!\*)([^*]+)(?<!\*)\*(?!\*)|(?<!_)_(?!_)([^_]+)(?<!_)_(?!_)")]
    private static partial Regex ItalicPattern();
}
