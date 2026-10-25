namespace MyBlog.Core.Interfaces;

public interface IMarkdownService
{
    Task<string> ToHtmlAsync(string markdown);
}
