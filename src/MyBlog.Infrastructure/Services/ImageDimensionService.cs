using System.Buffers.Binary;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyBlog.Core.Interfaces;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;

namespace MyBlog.Infrastructure.Services;

public sealed class ImageDimensionService : IImageDimensionService
{
    private readonly HttpClient _httpClient;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ImageDimensionService> _logger;

    public ImageDimensionService(
        HttpClient httpClient,
        IServiceScopeFactory scopeFactory,
        ILogger<ImageDimensionService> logger)
    {
        _httpClient = httpClient;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _httpClient.Timeout = TimeSpan.FromSeconds(5);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MyBlog/1.0");
    }

    public async Task<(int Width, int Height)?> GetDimensionsAsync(string url, CancellationToken cancellationToken = default)
    {
        // 1. Check Cache (with graceful error handling for missing table)
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
            
            // Check if table exists before querying
            if (await TableExistsAsync(db, cancellationToken))
            {
                var cached = await db.ImageDimensionCache.FindAsync([url], cancellationToken);
                if (cached != null)
                {
                    return (cached.Width, cached.Height);
                }
            }
        }
        catch (Exception ex)
        {
            // Log but don't fail - cache is optional
            _logger.LogWarning(ex, "Failed to check image dimension cache for {Url}. Continuing without cache.", url);
        }

        // 2. Fetch if missing
        try
        {
            var dimensions = await FetchDimensionsFromNetworkAsync(url, cancellationToken);

            if (dimensions.HasValue)
            {
                // 3. Update Cache (with graceful error handling)
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
                    
                    if (await TableExistsAsync(db, cancellationToken))
                    {
                        // Double check to prevent race conditions
                        if (!await db.ImageDimensionCache.AnyAsync(x => x.Url == url, cancellationToken))
                        {
                            db.ImageDimensionCache.Add(new ImageDimensionCache
                            {
                                Url = url,
                                Width = dimensions.Value.Width,
                                Height = dimensions.Value.Height,
                                LastCheckedUtc = DateTime.UtcNow
                            });
                            await db.SaveChangesAsync(cancellationToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log but don't fail - caching is optional
                    _logger.LogWarning(ex, "Failed to cache image dimensions for {Url}. Dimensions were resolved but not cached.", url);
                }
                
                return dimensions;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve dimensions for {Url}", url);
        }

        return null;
    }

    /// <summary>
    /// Checks if the ImageDimensionCache table exists in the database.
    /// </summary>
    private static async Task<bool> TableExistsAsync(BlogDbContext db, CancellationToken ct)
    {
        try
        {
            // For SQLite, check sqlite_master
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

    private async Task<(int Width, int Height)?> FetchDimensionsFromNetworkAsync(string url, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[32];
        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct);

        if (bytesRead < 8)
        {
            return null;
        }

        if (IsPng(buffer))
        {
            // PNG: Width at bytes 16-19, Height at bytes 20-23 (big-endian)
            if (bytesRead >= 24)
            {
                var width = BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(16, 4));
                var height = BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(20, 4));
                return (width, height);
            }
        }
        else if (IsGif(buffer))
        {
            // GIF: Width at bytes 6-7, Height at bytes 8-9 (little-endian)
            if (bytesRead >= 10)
            {
                var width = BinaryPrimitives.ReadInt16LittleEndian(buffer.AsSpan(6, 2));
                var height = BinaryPrimitives.ReadInt16LittleEndian(buffer.AsSpan(8, 2));
                return (width, height);
            }
        }
        else if (IsJpeg(buffer))
        {
            // JPEG requires scanning for SOF marker - more complex
            return await ParseJpegDimensionsAsync(stream, buffer, ct);
        }
        else if (IsWebP(buffer))
        {
            // WebP: Check for VP8 or VP8L chunk
            return await ParseWebPDimensionsAsync(stream, buffer, bytesRead, ct);
        }

        return null;
    }

    private static bool IsPng(ReadOnlySpan<byte> buffer) =>
        buffer.Length >= 8 &&
        buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47 &&
        buffer[4] == 0x0D && buffer[5] == 0x0A && buffer[6] == 0x1A && buffer[7] == 0x0A;

    private static bool IsGif(ReadOnlySpan<byte> buffer) =>
        buffer.Length >= 6 &&
        buffer[0] == 0x47 && buffer[1] == 0x49 && buffer[2] == 0x46 && // GIF
        buffer[3] == 0x38 && (buffer[4] == 0x39 || buffer[4] == 0x37) && buffer[5] == 0x61; // 89a or 87a

    private static bool IsJpeg(ReadOnlySpan<byte> buffer) =>
        buffer.Length >= 2 &&
        buffer[0] == 0xFF && buffer[1] == 0xD8;

    private static bool IsWebP(ReadOnlySpan<byte> buffer) =>
        buffer.Length >= 12 &&
        buffer[0] == 0x52 && buffer[1] == 0x49 && buffer[2] == 0x46 && buffer[3] == 0x46 && // RIFF
        buffer[8] == 0x57 && buffer[9] == 0x45 && buffer[10] == 0x42 && buffer[11] == 0x50; // WEBP

    private async Task<(int Width, int Height)?> ParseJpegDimensionsAsync(Stream stream, byte[] initialBuffer, CancellationToken ct)
    {
        // JPEG parsing - look for SOF0/SOF2 marker
        var buffer = new byte[8];
        var position = 2; // Skip SOI marker

        while (true)
        {
            // Read marker
            var read = await stream.ReadAsync(buffer.AsMemory(0, 2), ct);
            if (read < 2) return null;

            if (buffer[0] != 0xFF) return null;

            var marker = buffer[1];

            // Skip padding bytes
            while (marker == 0xFF)
            {
                read = await stream.ReadAsync(buffer.AsMemory(0, 1), ct);
                if (read < 1) return null;
                marker = buffer[0];
            }

            // SOF markers (SOF0, SOF1, SOF2, etc.)
            if (marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
            {
                // Read length (2 bytes) + precision (1 byte) + height (2 bytes) + width (2 bytes)
                read = await stream.ReadAsync(buffer.AsMemory(0, 7), ct);
                if (read < 7) return null;

                var height = BinaryPrimitives.ReadInt16BigEndian(buffer.AsSpan(3, 2));
                var width = BinaryPrimitives.ReadInt16BigEndian(buffer.AsSpan(5, 2));
                return (width, height);
            }

            // EOI marker - end of image
            if (marker == 0xD9) return null;

            // SOS marker - start of scan, no more metadata
            if (marker == 0xDA) return null;

            // Read segment length and skip
            read = await stream.ReadAsync(buffer.AsMemory(0, 2), ct);
            if (read < 2) return null;

            var length = BinaryPrimitives.ReadInt16BigEndian(buffer.AsSpan(0, 2)) - 2;
            if (length > 0)
            {
                // Skip the segment
                var skipBuffer = new byte[Math.Min(length, 4096)];
                var remaining = length;
                while (remaining > 0)
                {
                    var toRead = Math.Min(remaining, skipBuffer.Length);
                    read = await stream.ReadAsync(skipBuffer.AsMemory(0, toRead), ct);
                    if (read == 0) return null;
                    remaining -= read;
                }
            }
        }
    }

    private async Task<(int Width, int Height)?> ParseWebPDimensionsAsync(Stream stream, byte[] initialBuffer, int initialBytesRead, CancellationToken ct)
    {
        // Need more bytes for WebP parsing
        var buffer = new byte[30];
        Array.Copy(initialBuffer, buffer, initialBytesRead);
        
        if (initialBytesRead < 30)
        {
            var additionalRead = await stream.ReadAsync(buffer.AsMemory(initialBytesRead, 30 - initialBytesRead), ct);
            if (initialBytesRead + additionalRead < 30) return null;
        }

        // Check chunk type at offset 12
        var chunkType = System.Text.Encoding.ASCII.GetString(buffer, 12, 4);

        if (chunkType == "VP8 ")
        {
            // Lossy WebP - dimensions at offset 26-29
            var width = BinaryPrimitives.ReadInt16LittleEndian(buffer.AsSpan(26, 2)) & 0x3FFF;
            var height = BinaryPrimitives.ReadInt16LittleEndian(buffer.AsSpan(28, 2)) & 0x3FFF;
            return (width, height);
        }
        else if (chunkType == "VP8L")
        {
            // Lossless WebP - dimensions encoded in first 4 bytes after signature
            var b0 = buffer[21];
            var b1 = buffer[22];
            var b2 = buffer[23];
            var b3 = buffer[24];

            var width = (b0 | ((b1 & 0x3F) << 8)) + 1;
            var height = (((b1 & 0xC0) >> 6) | (b2 << 2) | ((b3 & 0x0F) << 10)) + 1;
            return (width, height);
        }
        else if (chunkType == "VP8X")
        {
            // Extended WebP - dimensions at offset 24-29
            var width = (buffer[24] | (buffer[25] << 8) | (buffer[26] << 16)) + 1;
            var height = (buffer[27] | (buffer[28] << 8) | (buffer[29] << 16)) + 1;
            return (width, height);
        }

        return null;
    }
}
