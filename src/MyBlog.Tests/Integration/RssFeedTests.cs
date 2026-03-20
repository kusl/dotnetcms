using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MyBlog.Core.Interfaces;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;
using MyBlog.Infrastructure.Repositories;
using Xunit;

namespace MyBlog.Tests.Integration;

/// <summary>
/// Integration tests for the RSS feed endpoint.
/// Uses in-memory SQLite for cross-platform compatibility.
/// Tests mirror the generation logic in RssEndpoints to validate output
/// without requiring a full ASP.NET Core pipeline.
/// </summary>
public class RssFeedTests : IAsyncDisposable
{
    private readonly BlogDbContext _context;
    private readonly PostRepository _postRepository;
    private readonly User _testUser;
    private readonly IConfiguration _configuration;

    private static readonly XNamespace AtomNs = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace ContentNs = "http://purl.org/rss/1.0/modules/content/";

    public RssFeedTests()
    {
        var options = new DbContextOptionsBuilder<BlogDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _context = new BlogDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _testUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            PasswordHash = "hash",
            Email = "test@example.com",
            DisplayName = "Test User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(_testUser);
        _context.SaveChanges();

        _postRepository = new PostRepository(_context);

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Application:Title"] = "TestBlog"
            })
            .Build();
    }

    public async ValueTask DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task RssFeed_GeneratesValidXml()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateTestPost("Test Post 1", isPublished: true, ct: ct);

        var bytes = await GenerateFeedBytes(ct);
        var xml = Encoding.UTF8.GetString(bytes);

        // Should parse without errors
        var doc = XDocument.Parse(xml);
        Assert.NotNull(doc.Root);
        Assert.Equal("rss", doc.Root.Name.LocalName);
    }

    [Fact]
    public async Task RssFeed_HasCorrectRssVersion()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateTestPost("Test Post", isPublished: true, ct: ct);

        var doc = await GenerateFeedXDocument(ct);

        Assert.Equal("2.0", doc.Root!.Attribute("version")?.Value);
    }

    [Fact]
    public async Task RssFeed_ContainsOnlyPublishedPosts()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateTestPost("Published Post 1", isPublished: true, ct: ct);
        await CreateTestPost("Published Post 2", isPublished: true, ct: ct);
        await CreateTestPost("Draft Post", isPublished: false, ct: ct);

        var doc = await GenerateFeedXDocument(ct);

        var items = doc.Descendants("item").ToList();
        Assert.Equal(2, items.Count);
        Assert.DoesNotContain(items, i => i.Element("title")?.Value == "Draft Post");
    }

    [Fact]
    public async Task RssFeed_HasChannelMetadata()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateTestPost("Test Post", isPublished: true, ct: ct);

        var doc = await GenerateFeedXDocument(ct);
        var channel = doc.Root!.Element("channel")!;

        Assert.Equal("TestBlog", channel.Element("title")?.Value);
        Assert.NotNull(channel.Element("link")?.Value);
        Assert.NotNull(channel.Element("description")?.Value);
        Assert.NotNull(channel.Element("language")?.Value);
    }

    [Fact]
    public async Task RssFeed_HasAtomSelfLink()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateTestPost("Test Post", isPublished: true, ct: ct);

        var doc = await GenerateFeedXDocument(ct);
        var channel = doc.Root!.Element("channel")!;

        var atomLink = channel.Element(AtomNs + "link");
        Assert.NotNull(atomLink);
        Assert.Equal("self", atomLink.Attribute("rel")?.Value);
        Assert.Equal("application/rss+xml", atomLink.Attribute("type")?.Value);
        Assert.Contains("/feed.xml", atomLink.Attribute("href")?.Value);
    }

    [Fact]
    public async Task RssFeed_ItemHasGuidWithPermalink()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateTestPost("Guid Test Post", slug: "guid-test-post", isPublished: true, ct: ct);

        var doc = await GenerateFeedXDocument(ct);
        var item = doc.Descendants("item").First();
        var guid = item.Element("guid");

        Assert.NotNull(guid);
        Assert.Equal("true", guid.Attribute("isPermaLink")?.Value);
        Assert.Contains("/post/guid-test-post", guid.Value);
    }

    [Fact]
    public async Task RssFeed_ItemHasRfc822PubDate()
    {
        var ct = TestContext.Current.CancellationToken;
        var publishedDate = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);
        await CreateTestPost("Date Test Post", isPublished: true, publishedAt: publishedDate, ct: ct);

        var doc = await GenerateFeedXDocument(ct);
        var item = doc.Descendants("item").First();
        var pubDate = item.Element("pubDate")?.Value;

        Assert.NotNull(pubDate);
        // RFC 822 format should be parseable
        Assert.True(DateTimeOffset.TryParse(pubDate, out _), $"pubDate '{pubDate}' should be a valid RFC 822 date");
        // RFC 822 dates produced by .ToString("R") contain "GMT"
        Assert.Contains("GMT", pubDate);
    }

    [Fact]
    public async Task RssFeed_ItemHasRequiredElements()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateTestPost("Complete Post", slug: "complete-post", isPublished: true, ct: ct);

        var doc = await GenerateFeedXDocument(ct);
        var item = doc.Descendants("item").First();

        Assert.NotNull(item.Element("title"));
        Assert.Equal("Complete Post", item.Element("title")!.Value);
        Assert.NotNull(item.Element("link"));
        Assert.Contains("/post/complete-post", item.Element("link")!.Value);
        Assert.NotNull(item.Element("description"));
        Assert.NotNull(item.Element("author"));
        Assert.Equal("Test User", item.Element("author")!.Value);
    }

    [Fact]
    public async Task RssFeed_WithNoPosts_GeneratesEmptyFeed()
    {
        var ct = TestContext.Current.CancellationToken;

        var doc = await GenerateFeedXDocument(ct);
        var items = doc.Descendants("item").ToList();

        Assert.Empty(items);
        // Channel metadata should still be present
        Assert.NotNull(doc.Root!.Element("channel")!.Element("title"));
    }

    [Fact]
    public async Task RssFeed_LimitsTo20Posts()
    {
        var ct = TestContext.Current.CancellationToken;

        // Create 25 published posts
        for (var i = 0; i < 25; i++)
        {
            await CreateTestPost($"Post {i}", isPublished: true, ct: ct);
        }

        var doc = await GenerateFeedXDocument(ct);
        var items = doc.Descendants("item").ToList();

        Assert.Equal(20, items.Count);
    }

    [Fact]
    public async Task RssFeed_HasNoBom()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateTestPost("BOM Test Post", isPublished: true, ct: ct);

        var bytes = await GenerateFeedBytes(ct);

        // UTF-8 BOM is EF BB BF
        Assert.False(
            bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
            "RSS feed should not contain a UTF-8 BOM");
    }

    [Fact]
    public async Task RssFeed_ItemHasContentEncoded_WithFullHtml()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateTestPost("Full Content Post", slug: "full-content-post", isPublished: true,
            content: "# Hello World\n\nThis is the **full** post content.", ct: ct);

        var doc = await GenerateFeedXDocument(ct);
        var item = doc.Descendants("item").First();
        var contentEncoded = item.Element(ContentNs + "encoded");

        Assert.NotNull(contentEncoded);
        var html = contentEncoded.Value;
        // The StubMarkdownService wraps in <p> tags — just verify content is present
        Assert.Contains("Hello World", html);
        Assert.Contains("full", html);
    }

    [Fact]
    public async Task RssFeed_ContentEncoded_IsCData()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateTestPost("CData Test", slug: "cdata-test", isPublished: true,
            content: "Some <em>content</em> here", ct: ct);

        var bytes = await GenerateFeedBytes(ct);
        var xml = Encoding.UTF8.GetString(bytes);

        // The raw XML should contain CDATA wrapping around content:encoded value
        Assert.Contains("<![CDATA[", xml);
    }

    [Fact]
    public async Task RssFeed_DescriptionIsSummary_NotFullContent()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateTestPost("Summary Test", slug: "summary-test", isPublished: true,
            content: "This is a very long post body that should not appear in description",
            summary: "Short summary", ct: ct);

        var doc = await GenerateFeedXDocument(ct);
        var item = doc.Descendants("item").First();

        Assert.Equal("Short summary", item.Element("description")?.Value);
    }

    [Fact]
    public async Task RssFeed_ContentEncoded_ContainsRenderedMarkdown()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateTestPost("Markdown Test", slug: "markdown-test", isPublished: true,
            content: "**bold text** and *italic text*", ct: ct);

        var doc = await GenerateFeedXDocument(ct);
        var item = doc.Descendants("item").First();
        var contentEncoded = item.Element(ContentNs + "encoded");

        Assert.NotNull(contentEncoded);
        var html = contentEncoded.Value;
        // Our stub returns content wrapped in <p> tags
        Assert.Contains("<p>", html);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    /// <summary>
    /// Generates RSS feed bytes using the same logic as RssEndpoints
    /// but without requiring a full ASP.NET Core pipeline.
    /// </summary>
    private async Task<byte[]> GenerateFeedBytes(CancellationToken ct)
    {
        var baseUrl = "https://example.com";
        var siteTitle = _configuration["Application:Title"] ?? "MyBlog";
        var feedUrl = $"{baseUrl}/feed.xml";
        var markdownService = new StubMarkdownService();

        var (posts, _) = await _postRepository.GetPublishedPostsAsync(1, 20, ct);

        // Fetch full content for each post, mirroring the endpoint logic
        var fullPosts = new List<(PostListItemDto ListItem, PostDetailDto? Detail)>();
        foreach (var post in posts)
        {
            var detail = await _postRepository.GetBySlugAsync(post.Slug, ct);
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

            await writer.WriteStartElementAsync(null, "channel", null);

            await writer.WriteElementStringAsync(null, "title", null, siteTitle);
            await writer.WriteElementStringAsync(null, "link", null, baseUrl);
            await writer.WriteElementStringAsync(null, "description", null, $"{siteTitle} — Recent posts");
            await writer.WriteElementStringAsync(null, "language", null, "en-us");
            await writer.WriteElementStringAsync(null, "generator", null, "MyBlog/.NET 10");
            await writer.WriteElementStringAsync(null, "lastBuildDate", null, DateTime.UtcNow.ToString("R"));

            await writer.WriteStartElementAsync("atom", "link", "http://www.w3.org/2005/Atom");
            await writer.WriteAttributeStringAsync(null, "href", null, feedUrl);
            await writer.WriteAttributeStringAsync(null, "rel", null, "self");
            await writer.WriteAttributeStringAsync(null, "type", null, "application/rss+xml");
            await writer.WriteEndElementAsync();

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

                await writer.WriteStartElementAsync(null, "guid", null);
                await writer.WriteAttributeStringAsync(null, "isPermaLink", null, "true");
                await writer.WriteStringAsync(permalink);
                await writer.WriteEndElementAsync();

                // <content:encoded> — full rendered HTML
                if (detail is not null)
                {
                    var htmlContent = await markdownService.ToHtmlAsync(detail.Content);
                    await writer.WriteStartElementAsync("content", "encoded", "http://purl.org/rss/1.0/modules/content/");
                    await writer.WriteCDataAsync(htmlContent);
                    await writer.WriteEndElementAsync();
                }

                await writer.WriteEndElementAsync();
            }

            await writer.WriteEndElementAsync();
            await writer.WriteEndElementAsync();
            await writer.WriteEndDocumentAsync();

            await writer.FlushAsync();
        }

        return stream.ToArray();
    }

    private async Task<XDocument> GenerateFeedXDocument(CancellationToken ct)
    {
        var bytes = await GenerateFeedBytes(ct);
        var xml = Encoding.UTF8.GetString(bytes);
        return XDocument.Parse(xml);
    }

    private async Task CreateTestPost(
        string title,
        string? slug = null,
        bool isPublished = true,
        DateTime? publishedAt = null,
        string? content = null,
        string? summary = null,
        CancellationToken ct = default)
    {
        var post = new Post
        {
            Id = Guid.NewGuid(),
            Title = title,
            Slug = slug ?? title.ToLower().Replace(" ", "-"),
            Content = content ?? $"Content for {title}",
            Summary = summary ?? $"Summary for {title}",
            AuthorId = _testUser.Id,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            IsPublished = isPublished,
            PublishedAtUtc = isPublished ? (publishedAt ?? DateTime.UtcNow) : null
        };

        _context.Posts.Add(post);
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Minimal stub for IMarkdownService used by tests.
    /// Wraps content in paragraph tags to simulate basic HTML rendering.
    /// </summary>
    private sealed class StubMarkdownService : IMarkdownService
    {
        public Task<string> ToHtmlAsync(string markdown) =>
            Task.FromResult($"<p>{markdown}</p>");
    }
}
