using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyBlog.Core.Interfaces;
using MyBlog.Infrastructure.Data;

namespace MyBlog.Infrastructure.Services;

public sealed class ImageCacheWarmerService(
    IServiceScopeFactory scopeFactory,
    ILogger<ImageCacheWarmerService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        logger.LogInformation("Image Cache Warmer started. Scanning posts for uncached images...");

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
            var dimService = scope.ServiceProvider.GetRequiredService<IImageDimensionService>();

            var regex = new Regex(@"!\[([^\]]*)\]\(([^)]+)\)");
            var distinctUrls = new HashSet<string>();

            await foreach (var content in db.Posts
                .Select(p => p.Content)
                .AsAsyncEnumerable()
                .WithCancellation(stoppingToken))
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
                logger.LogWarning(ex, "Image Cache Warmer: Could not read existing cache. Will attempt to cache all URLs.");
                existingSet = new HashSet<string>();
            }

            var missingUrls = distinctUrls.Where(u => !existingSet.Contains(u)).ToList();

            if (missingUrls.Count == 0)
            {
                logger.LogInformation("Image Cache Warmer: All images are already cached.");
                return;
            }

            logger.LogInformation("Image Cache Warmer: Found {Count} uncached images. Fetching dimensions...", missingUrls.Count);

            var successCount = 0;
            var failCount = 0;

            foreach (var url in missingUrls.TakeWhile(_ => !stoppingToken.IsCancellationRequested))
            {
                try
                {
                    var dimensions = await dimService.GetDimensionsAsync(url, stoppingToken);
                    if (dimensions.HasValue)
                    {
                        logger.LogDebug("Cached dimensions for: {Url} ({Width}x{Height})",
                            url, dimensions.Value.Width, dimensions.Value.Height);
                        successCount++;
                    }
                    else
                    {
                        logger.LogDebug("Could not resolve dimensions for: {Url}", url);
                        failCount++;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Error warming cache for {Url}", url);
                    failCount++;
                }

                await Task.Delay(100, stoppingToken);
            }

            logger.LogInformation("Image Cache Warmer completed. Cached: {Success}, Failed: {Failed}",
                successCount, failCount);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Image Cache Warmer was cancelled.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in Image Cache Warmer. This is non-fatal - the blog will continue to work without cached dimensions.");
        }
    }
}
