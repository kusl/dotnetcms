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
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
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

        await using var stream = await response.Content.ReadAsStreamAsync(ct);

        // For JPEG, we need to scan through the file looking for SOF markers
        // For PNG/GIF/WebP, dimensions are at fixed offsets near the start
        // Read enough bytes to handle all formats' headers
        var buffer = new byte[32];
        var bytesRead = await ReadFullyAsync(stream, buffer, ct);

        if (bytesRead < 8)
        {
            return null;
        }

        if (IsPng(buffer))
        {
            // PNG: IHDR chunk starts at byte 8, width at 16-19, height at 20-23 (big-endian)
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
            // JPEG requires scanning for SOF marker
            // We need to parse from the beginning, so use a buffered approach
            return await ParseJpegDimensionsAsync(stream, buffer, bytesRead, ct);
        }
        else if (IsWebP(buffer))
        {
            // WebP: Check for VP8, VP8L, or VP8X chunk
            return ParseWebPDimensions(buffer, bytesRead);
        }

        return null;
    }

    /// <summary>
    /// Reads exactly the requested number of bytes, or as many as available.
    /// Handles cases where ReadAsync returns fewer bytes than requested.
    /// </summary>
    private static async Task<int> ReadFullyAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), ct);
            if (read == 0)
            {
                break; // End of stream
            }
            totalRead += read;
        }
        return totalRead;
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

    /// <summary>
    /// Parses JPEG dimensions by scanning for SOF (Start of Frame) markers.
    /// JPEG structure: Starts with FFD8, then segments each starting with FF XX (marker).
    /// SOF markers (FFC0-FFCF, excluding FFC4, FFC8, FFCC) contain dimensions.
    /// </summary>
    private async Task<(int Width, int Height)?> ParseJpegDimensionsAsync(
        Stream stream, byte[] initialBuffer, int initialBytesRead, CancellationToken ct)
    {
        // Create a combined stream that first reads from initialBuffer, then continues from network stream
        // We already have 'initialBytesRead' bytes in 'initialBuffer', stream is positioned after those bytes

        // Start parsing from byte 2 (after FF D8 SOI marker)
        var position = 2;

        // Helper to read bytes, first from initialBuffer then from stream
        async Task<int> ReadByteAsync()
        {
            if (position < initialBytesRead)
            {
                return initialBuffer[position++];
            }

            var b = new byte[1];
            var read = await stream.ReadAsync(b, 0, 1, ct);
            if (read == 0)
            {
                return -1; // EOF
            }
            position++;
            return b[0];
        }

        async Task<byte[]?> ReadBytesAsync(int count)
        {
            var result = new byte[count];
            var resultPos = 0;

            // First, read from initialBuffer
            while (resultPos < count && position < initialBytesRead)
            {
                result[resultPos++] = initialBuffer[position++];
            }

            // Then read remaining from stream
            if (resultPos < count)
            {
                var remaining = count - resultPos;
                var read = await ReadFullyAsync(stream, result.AsMemory(resultPos, remaining), ct);
                if (read < remaining)
                {
                    return null; // EOF before we got all bytes
                }
                position += read;
            }

            return result;
        }

        // Scan for SOF marker
        while (true)
        {
            // Read marker (FF XX)
            var ff = await ReadByteAsync();
            if (ff == -1)
            {
                return null; // EOF
            }

            if (ff != 0xFF)
            {
                // Not a marker, keep scanning
                continue;
            }

            // Skip any padding FF bytes
            int marker;
            do
            {
                marker = await ReadByteAsync();
                if (marker == -1)
                {
                    return null; // EOF
                }
            } while (marker == 0xFF);

            // Check for SOF markers (C0-CF except C4, C8, CC)
            if (marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
            {
                // Found SOF marker! Read length (2) + precision (1) + height (2) + width (2)
                var sofData = await ReadBytesAsync(7);
                if (sofData == null)
                {
                    return null;
                }

                var height = BinaryPrimitives.ReadInt16BigEndian(sofData.AsSpan(3, 2));
                var width = BinaryPrimitives.ReadInt16BigEndian(sofData.AsSpan(5, 2));
                return (width, height);
            }

            // EOI (End of Image) - no dimensions found
            if (marker == 0xD9)
            {
                return null;
            }

            // SOS (Start of Scan) - image data follows, no more metadata
            if (marker == 0xDA)
            {
                return null;
            }

            // RST markers (D0-D7) and standalone markers - no length field
            if ((marker >= 0xD0 && marker <= 0xD7) || marker == 0x00 || marker == 0x01)
            {
                continue;
            }

            // Other markers have a length field - skip the segment
            var lengthBytes = await ReadBytesAsync(2);
            if (lengthBytes == null)
            {
                return null;
            }

            var length = BinaryPrimitives.ReadInt16BigEndian(lengthBytes) - 2; // Length includes itself
            if (length > 0)
            {
                // Skip segment content
                if (position < initialBytesRead)
                {
                    // Skip what we can from initialBuffer
                    var skipFromBuffer = Math.Min(length, initialBytesRead - position);
                    position += skipFromBuffer;
                    length -= skipFromBuffer;
                }

                // Skip remaining from stream
                if (length > 0)
                {
                    var skipBuffer = new byte[Math.Min(length, 8192)];
                    var remaining = length;
                    while (remaining > 0)
                    {
                        var toRead = Math.Min(remaining, skipBuffer.Length);
                        var read = await ReadFullyAsync(stream, skipBuffer.AsMemory(0, toRead), ct);
                        if (read == 0)
                        {
                            return null; // EOF
                        }
                        remaining -= read;
                        position += read;
                    }
                }
            }
        }
    }

    private static async Task<int> ReadFullyAsync(Stream stream, Memory<byte> buffer, CancellationToken ct)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[totalRead..], ct);
            if (read == 0)
            {
                break;
            }
            totalRead += read;
        }
        return totalRead;
    }

    /// <summary>
    /// Parses WebP dimensions from the initial buffer.
    /// WebP has three formats: VP8 (lossy), VP8L (lossless), VP8X (extended).
    /// </summary>
    private static (int Width, int Height)? ParseWebPDimensions(byte[] buffer, int bytesRead)
    {
        if (bytesRead < 30)
        {
            return null;
        }

        // Check chunk type at offset 12
        var chunkType = System.Text.Encoding.ASCII.GetString(buffer, 12, 4);

        switch (chunkType)
        {
            case "VP8 ":
            {
                // Lossy WebP
                // VP8 bitstream starts at offset 20
                // Frame tag at bytes 20-22, then dimensions
                // Signature bytes: 9D 01 2A
                if (bytesRead >= 30 && buffer[23] == 0x9D && buffer[24] == 0x01 && buffer[25] == 0x2A)
                {
                    // Width at 26-27 (little-endian, 14 bits)
                    // Height at 28-29 (little-endian, 14 bits)
                    var width = (buffer[26] | (buffer[27] << 8)) & 0x3FFF;
                    var height = (buffer[28] | (buffer[29] << 8)) & 0x3FFF;
                    return (width, height);
                }
                break;
            }
            case "VP8L":
            {
                // Lossless WebP
                // Signature byte at offset 20: 0x2F
                if (bytesRead >= 25 && buffer[20] == 0x2F)
                {
                    // Dimensions encoded in bytes 21-24
                    // Width: 14 bits starting at bit 0
                    // Height: 14 bits starting at bit 14
                    var b0 = buffer[21];
                    var b1 = buffer[22];
                    var b2 = buffer[23];
                    var b3 = buffer[24];

                    var width = ((b0 | (b1 << 8)) & 0x3FFF) + 1;
                    var height = ((((b1 >> 6) | (b2 << 2) | (b3 << 10))) & 0x3FFF) + 1;
                    return (width, height);
                }
                break;
            }
            case "VP8X":
            {
                // Extended WebP
                // Canvas size at offset 24-29
                if (bytesRead >= 30)
                {
                    // Width at 24-26 (24-bit little-endian) + 1
                    // Height at 27-29 (24-bit little-endian) + 1
                    var width = (buffer[24] | (buffer[25] << 8) | (buffer[26] << 16)) + 1;
                    var height = (buffer[27] | (buffer[28] << 8) | (buffer[29] << 16)) + 1;
                    return (width, height);
                }
                break;
            }
        }

        return null;
    }
}
