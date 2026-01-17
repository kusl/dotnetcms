using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyBlog.Core.Interfaces;
using MyBlog.Infrastructure.Data;
using System.Text.RegularExpressions;

namespace MyBlog.Infrastructure.Services;

/// <summary>
/// Scans all posts on startup to ensure image dimensions are cached.
/// This automates the fix for "past" images.
/// </summary>
public sealed class ImageCacheWarmerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ImageCacheWarmerService> _logger;

    public ImageCacheWarmerService(
        IServiceScopeFactory scopeFactory,
        ILogger<ImageCacheWarmerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Image Cache Warmer started. Scanning posts for uncached images...");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
            var dimService = scope.ServiceProvider.GetRequiredService<IImageDimensionService>();

            // Get all posts
            var posts = await db.Posts
                .Select(p => p.Content)
                .ToListAsync(stoppingToken);

            var regex = new Regex(@"!\[([^\]]*)\]\(([^)]+)\)");
            var distinctUrls = new HashSet<string>();

            // 1. Extract all URLs from all posts
            foreach (var content in posts)
            {
                if (string.IsNullOrEmpty(content))
                {
                    continue;
                }

                var matches = regex.Matches(content);
                foreach (Match match in matches)
                {
                    distinctUrls.Add(match.Groups[2].Value);
                }
            }

            // 2. Filter out ones we already have
            var existingCache = await db.ImageDimensionCache
                .Select(x => x.Url)
                .ToListAsync(stoppingToken);

            var existingSet = new HashSet<string>(existingCache);
            var missingUrls = distinctUrls.Where(u => !existingSet.Contains(u)).ToList();

            if (missingUrls.Count == 0)
            {
                _logger.LogInformation("Image Cache Warmer: All images are already cached.");
                return;
            }

            _logger.LogInformation("Image Cache Warmer: Found {Count} uncached images. Fetching dimensions...", missingUrls.Count);

            // 3. Process missing (in parallel but throttled)
            foreach (var url in missingUrls.TakeWhile(url => !stoppingToken.IsCancellationRequested))
            {
                try
                {
                    // Calling GetDimensionsAsync triggers the logic to fetch and save to DB
                    await dimService.GetDimensionsAsync(url, stoppingToken);
                    _logger.LogInformation("Cached dimensions for: {Url}", url);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error warming cache for {Url}", url);
                }

                // Be gentle on remote servers
                await Task.Delay(100, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Image Cache Warmer");
        }
    }
}
