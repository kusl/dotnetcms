namespace MyBlog.Core.Interfaces;

/// <summary>
/// Service interface for rendering Markdown to HTML.
/// </summary>
public interface IMarkdownService
{
    /// <summary>Converts Markdown text to HTML.</summary>
    string ToHtml(string markdown);
}
