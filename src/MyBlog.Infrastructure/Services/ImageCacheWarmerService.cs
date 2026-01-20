using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyBlog.Core.Interfaces;
using MyBlog.Infrastructure.Data;

namespace MyBlog.Infrastructure.Services;

/// <summary>
/// Background service that warms the image dimension cache on startup.
/// Scans all posts for images and pre-fetches their dimensions.
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
        // Give the app time to fully start up
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        _logger.LogInformation("Image Cache Warmer started. Scanning posts for uncached images...");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
            var dimService = scope.ServiceProvider.GetRequiredService<IImageDimensionService>();

            // Check if the ImageDimensionCache table exists
            if (!await TableExistsAsync(db, stoppingToken))
            {
                _logger.LogWarning("Image Cache Warmer: ImageDimensionCache table does not exist. " +
                    "Please apply database migrations. Skipping cache warming.");
                return;
            }

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
            HashSet<string> existingSet;
            try
            {
                var existingCache = await db.ImageDimensionCache
                    .Select(x => x.Url)
                    .ToListAsync(stoppingToken);
                existingSet = new HashSet<string>(existingCache);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Image Cache Warmer: Could not read existing cache. Will attempt to cache all URLs.");
                existingSet = new HashSet<string>();
            }

            var missingUrls = distinctUrls.Where(u => !existingSet.Contains(u)).ToList();

            if (missingUrls.Count == 0)
            {
                _logger.LogInformation("Image Cache Warmer: All images are already cached.");
                return;
            }

            _logger.LogInformation("Image Cache Warmer: Found {Count} uncached images. Fetching dimensions...", missingUrls.Count);

            // 3. Process missing (in parallel but throttled)
            var successCount = 0;
            var failCount = 0;

            foreach (var url in missingUrls.TakeWhile(_ => !stoppingToken.IsCancellationRequested))
            {
                try
                {
                    // Calling GetDimensionsAsync triggers the logic to fetch and save to DB
                    var dimensions = await dimService.GetDimensionsAsync(url, stoppingToken);
                    if (dimensions.HasValue)
                    {
                        _logger.LogDebug("Cached dimensions for: {Url} ({Width}x{Height})",
                            url, dimensions.Value.Width, dimensions.Value.Height);
                        successCount++;
                    }
                    else
                    {
                        _logger.LogDebug("Could not resolve dimensions for: {Url}", url);
                        failCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error warming cache for {Url}", url);
                    failCount++;
                }

                // Be gentle on remote servers
                await Task.Delay(100, stoppingToken);
            }

            _logger.LogInformation("Image Cache Warmer completed. Cached: {Success}, Failed: {Failed}",
                successCount, failCount);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Image Cache Warmer was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Image Cache Warmer. This is non-fatal - the blog will continue to work without cached dimensions.");
        }
    }

    /// <summary>
    /// Checks if the ImageDimensionCache table exists in the database.
    /// </summary>
    private static async Task<bool> TableExistsAsync(BlogDbContext db, CancellationToken ct)
    {
        try
        {
            var connection = db.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync(ct);
            }

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='ImageDimensionCache'";
            var result = await command.ExecuteScalarAsync(ct);
            return Convert.ToInt64(result) > 0;
        }
        catch
        {
            return false;
        }
    }
}
