using System.Text;
using System.Xml;
using MyBlog.Core.Interfaces;

namespace MyBlog.Web.Endpoints;

/// <summary>
/// Minimal API endpoints for the RSS 2.0 feed.
/// </summary>
public static class RssEndpoints
{
    private const int FeedItemLimit = 20;

    /// <summary>
    /// Maps the RSS feed endpoint at /feed.xml.
    /// </summary>
    public static IEndpointRouteBuilder MapRssEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/feed.xml", GenerateRssFeed)
            .CacheOutput(policy => policy.Expire(TimeSpan.FromMinutes(10)));

        return endpoints;
    }

    private static async Task<IResult> GenerateRssFeed(
        HttpContext context,
        IPostRepository postRepository,
        IConfiguration configuration,
        IMarkdownService markdownService)
    {
        var request = context.Request;
        var baseUrl = $"{request.Scheme}://{request.Host}";
        var siteTitle = configuration["Application:Title"] ?? "MyBlog";
        var feedUrl = $"{baseUrl}/feed.xml";

        var (posts, _) = await postRepository.GetPublishedPostsAsync(1, FeedItemLimit);

        // Fetch full content for each post via slug lookup.
        // With a cap of 20 posts and SQLite, this is perfectly fine.
        var fullPosts = new List<(Core.Models.PostListItemDto ListItem, Core.Models.PostDetailDto? Detail)>();
        foreach (var post in posts)
        {
            var detail = await postRepository.GetBySlugAsync(post.Slug);
            fullPosts.Add((post, detail));
        }

        var settings = new XmlWriterSettings
        {
            Async = true,
            Indent = true,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
        };

        using var stream = new MemoryStream();
        await using (var writer = XmlWriter.Create(stream, settings))
        {
            await writer.WriteStartDocumentAsync();

            await writer.WriteStartElementAsync(null, "rss", null);
            await writer.WriteAttributeStringAsync(null, "version", null, "2.0");
            await writer.WriteAttributeStringAsync("xmlns", "atom", null, "http://www.w3.org/2005/Atom");
            await writer.WriteAttributeStringAsync("xmlns", "content", null, "http://purl.org/rss/1.0/modules/content/");

            // <channel>
            await writer.WriteStartElementAsync(null, "channel", null);

            await writer.WriteElementStringAsync(null, "title", null, siteTitle);
            await writer.WriteElementStringAsync(null, "link", null, baseUrl);
            await writer.WriteElementStringAsync(null, "description", null, $"{siteTitle} — Recent posts");
            await writer.WriteElementStringAsync(null, "language", null, "en-us");
            await writer.WriteElementStringAsync(null, "generator", null, "MyBlog/.NET 10");
            await writer.WriteElementStringAsync(null, "lastBuildDate", null, DateTime.UtcNow.ToString("R"));

            // <atom:link rel="self">
            await writer.WriteStartElementAsync("atom", "link", "http://www.w3.org/2005/Atom");
            await writer.WriteAttributeStringAsync(null, "href", null, feedUrl);
            await writer.WriteAttributeStringAsync(null, "rel", null, "self");
            await writer.WriteAttributeStringAsync(null, "type", null, "application/rss+xml");
            await writer.WriteEndElementAsync(); // atom:link

            foreach (var (post, detail) in fullPosts)
            {
                var permalink = $"{baseUrl}/post/{post.Slug}";

                await writer.WriteStartElementAsync(null, "item", null);

                await writer.WriteElementStringAsync(null, "title", null, post.Title);
                await writer.WriteElementStringAsync(null, "link", null, permalink);
                await writer.WriteElementStringAsync(null, "description", null, post.Summary);

                if (post.PublishedAtUtc.HasValue)
                {
                    await writer.WriteElementStringAsync(null, "pubDate", null, post.PublishedAtUtc.Value.ToString("R"));
                }

                await writer.WriteElementStringAsync(null, "author", null, post.AuthorDisplayName);

                // <guid isPermaLink="true">
                await writer.WriteStartElementAsync(null, "guid", null);
                await writer.WriteAttributeStringAsync(null, "isPermaLink", null, "true");
                await writer.WriteStringAsync(permalink);
                await writer.WriteEndElementAsync(); // guid

                // <content:encoded> — full rendered HTML so readers can read offline
                if (detail is not null)
                {
                    var htmlContent = await markdownService.ToHtmlAsync(detail.Content);
                    await writer.WriteStartElementAsync("content", "encoded", "http://purl.org/rss/1.0/modules/content/");
                    await writer.WriteCDataAsync(htmlContent);
                    await writer.WriteEndElementAsync(); // content:encoded
                }

                await writer.WriteEndElementAsync(); // item
            }

            await writer.WriteEndElementAsync(); // channel
            await writer.WriteEndElementAsync(); // rss
            await writer.WriteEndDocumentAsync();

            await writer.FlushAsync();
        }

        var bytes = stream.ToArray();
        return Results.Bytes(bytes, "application/rss+xml; charset=utf-8");
    }
}
