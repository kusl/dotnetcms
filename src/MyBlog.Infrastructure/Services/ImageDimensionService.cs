using System.Buffers.Binary;
using System.Text;
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
    }

    public async Task<(int Width, int Height)?> GetDimensionsAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
            var cached = await db.ImageDimensionCache.FindAsync([url], cancellationToken);
            if (cached != null)
            {
                return (cached.Width, cached.Height);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check image dimension cache for {Url}. Continuing without cache.", url);
        }

        try
        {
            var dimensions = await FetchDimensionsFromNetworkAsync(url, cancellationToken);

            if (dimensions.HasValue)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();

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
                catch (Exception ex)
                {
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

    private async Task<(int Width, int Height)?> FetchDimensionsFromNetworkAsync(string url, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);

        var buffer = new byte[32];
        var bytesRead = await ReadFullyAsync(stream, buffer, ct);

        if (bytesRead < 8)
        {
            return null;
        }

        if (IsPng(buffer))
        {
            if (bytesRead >= 24)
            {
                var width = BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(16, 4));
                var height = BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(20, 4));
                return (width, height);
            }
        }
        else if (IsGif(buffer))
        {
            if (bytesRead >= 10)
            {
                var width = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(6, 2));
                var height = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(8, 2));
                return (width, height);
            }
        }
        else if (IsJpeg(buffer))
        {
            return await ParseJpegDimensionsAsync(stream, buffer, bytesRead, ct);
        }
        else if (IsWebP(buffer))
        {
            return ParseWebPDimensions(buffer, bytesRead);
        }

        return null;
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

    private static bool IsPng(ReadOnlySpan<byte> buffer) =>
        buffer.Length >= 8 &&
        buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47 &&
        buffer[4] == 0x0D && buffer[5] == 0x0A && buffer[6] == 0x1A && buffer[7] == 0x0A;

    private static bool IsGif(ReadOnlySpan<byte> buffer) =>
        buffer.Length >= 6 &&
        buffer[0] == 0x47 && buffer[1] == 0x49 && buffer[2] == 0x46 &&
        buffer[3] == 0x38 && (buffer[4] == 0x39 || buffer[4] == 0x37) && buffer[5] == 0x61;

    private static bool IsJpeg(ReadOnlySpan<byte> buffer) =>
        buffer.Length >= 2 &&
        buffer[0] == 0xFF && buffer[1] == 0xD8;

    private static bool IsWebP(ReadOnlySpan<byte> buffer) =>
        buffer.Length >= 12 &&
        buffer[0] == 0x52 && buffer[1] == 0x49 && buffer[2] == 0x46 && buffer[3] == 0x46 &&
        buffer[8] == 0x57 && buffer[9] == 0x45 && buffer[10] == 0x42 && buffer[11] == 0x50;

    private static async Task<(int Width, int Height)?> ParseJpegDimensionsAsync(
        Stream stream, byte[] initialBuffer, int initialBytesRead, CancellationToken ct)
    {
        var position = 2;
        var single = new byte[1];

        async Task<int> ReadByteAsync()
        {
            if (position < initialBytesRead)
            {
                return initialBuffer[position++];
            }

            var read = await stream.ReadAsync(single.AsMemory(0, 1), ct);
            if (read == 0)
            {
                return -1;
            }
            position++;
            return single[0];
        }

        async Task<byte[]?> ReadBytesAsync(int count)
        {
            var result = new byte[count];
            var resultPos = 0;

            while (resultPos < count && position < initialBytesRead)
            {
                result[resultPos++] = initialBuffer[position++];
            }

            if (resultPos < count)
            {
                var remaining = count - resultPos;
                var read = await ReadFullyAsync(stream, result.AsMemory(resultPos, remaining), ct);
                if (read < remaining)
                {
                    return null;
                }
                position += read;
            }

            return result;
        }

        while (true)
        {
            var ff = await ReadByteAsync();
            if (ff == -1)
            {
                return null;
            }

            if (ff != 0xFF)
            {
                continue;
            }

            int marker;
            do
            {
                marker = await ReadByteAsync();
                if (marker == -1)
                {
                    return null;
                }
            } while (marker == 0xFF);

            if (marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
            {
                var sofData = await ReadBytesAsync(7);
                if (sofData == null)
                {
                    return null;
                }

                var height = BinaryPrimitives.ReadUInt16BigEndian(sofData.AsSpan(3, 2));
                var width = BinaryPrimitives.ReadUInt16BigEndian(sofData.AsSpan(5, 2));
                return (width, height);
            }

            if (marker == 0xD9)
            {
                return null;
            }

            if (marker == 0xDA)
            {
                return null;
            }

            if ((marker >= 0xD0 && marker <= 0xD7) || marker == 0x00 || marker == 0x01)
            {
                continue;
            }

            var lengthBytes = await ReadBytesAsync(2);
            if (lengthBytes == null)
            {
                return null;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(lengthBytes) - 2;
            if (length > 0)
            {
                if (position < initialBytesRead)
                {
                    var skipFromBuffer = Math.Min(length, initialBytesRead - position);
                    position += skipFromBuffer;
                    length -= skipFromBuffer;
                }

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
                            return null;
                        }
                        remaining -= read;
                        position += read;
                    }
                }
            }
        }
    }

    private static (int Width, int Height)? ParseWebPDimensions(byte[] buffer, int bytesRead)
    {
        if (bytesRead < 30)
        {
            return null;
        }

        var chunkType = Encoding.ASCII.GetString(buffer, 12, 4);

        switch (chunkType)
        {
            case "VP8 ":
                {
                    if (buffer[23] == 0x9D && buffer[24] == 0x01 && buffer[25] == 0x2A)
                    {
                        var width = (buffer[26] | (buffer[27] << 8)) & 0x3FFF;
                        var height = (buffer[28] | (buffer[29] << 8)) & 0x3FFF;
                        return (width, height);
                    }
                    break;
                }
            case "VP8L":
                {
                    if (buffer[20] == 0x2F)
                    {
                        var b0 = buffer[21];
                        var b1 = buffer[22];
                        var b2 = buffer[23];
                        var b3 = buffer[24];

                        var width = ((b0 | (b1 << 8)) & 0x3FFF) + 1;
                        var height = (((b1 >> 6) | (b2 << 2) | (b3 << 10)) & 0x3FFF) + 1;
                        return (width, height);
                    }
                    break;
                }
            case "VP8X":
                {
                    var width = (buffer[24] | (buffer[25] << 8) | (buffer[26] << 16)) + 1;
                    var height = (buffer[27] | (buffer[28] << 8) | (buffer[29] << 16)) + 1;
                    return (width, height);
                }
        }

        return null;
    }
}
