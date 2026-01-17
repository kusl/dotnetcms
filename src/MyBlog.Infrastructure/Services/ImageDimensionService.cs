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
        // 1. Check Cache
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
            var cached = await db.ImageDimensionCache.FindAsync([url], cancellationToken);
            if (cached != null)
            {
                return (cached.Width, cached.Height);
            }
        }

        // 2. Fetch if missing
        try
        {
            var dimensions = await FetchDimensionsFromNetworkAsync(url, cancellationToken);

            if (dimensions.HasValue)
            {
                // 3. Update Cache
                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
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

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[32];
        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct);

        if (bytesRead < 8)
        {
            return null;
        }

        if (IsPng(buffer))
        {
            // PNG: Width at 16, Height at 20 (Big Endian)
            // We need slightly more than 24 bytes for IHDR
            return (
                BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(16, 4)),
                BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(20, 4))
            );
        }

        if (IsGif(buffer))
        {
            // GIF: Width at 6, Height at 8 (Little Endian)
            return (
                BinaryPrimitives.ReadInt16LittleEndian(buffer.AsSpan(6, 2)),
                BinaryPrimitives.ReadInt16LittleEndian(buffer.AsSpan(8, 2))
            );
        }

        if (IsBmp(buffer))
        {
            // BMP: Width at 18, Height at 22 (Little Endian)
            return (
                BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(18, 4)),
                BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(22, 4))
            );
        }

        if (IsWebP(buffer))
        {
            // Simple VP8X check
            if (buffer[12] == 'V' && buffer[13] == 'P' && buffer[14] == '8' && buffer[15] == 'X')
            {
                var width = buffer[24] | (buffer[25] << 8) | (buffer[26] << 16);
                var height = buffer[27] | (buffer[28] << 8) | (buffer[29] << 16);
                return (width + 1, height + 1);
            }
        }

        if (IsJpeg(buffer))
        {
            return await ParseJpegAsync(stream, buffer, bytesRead);
        }

        return null;
    }

    private static bool IsPng(byte[] b) => b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47;
    private static bool IsGif(byte[] b) => b[0] == 'G' && b[1] == 'I' && b[2] == 'F';
    private static bool IsBmp(byte[] b) => b[0] == 0x42 && b[1] == 0x4D;
    private static bool IsWebP(byte[] b) => b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P';
    private static bool IsJpeg(byte[] b) => b[0] == 0xFF && b[1] == 0xD8;

    private async Task<(int, int)?> ParseJpegAsync(Stream stream, byte[] buffer, int bufferLength)
    {
        int pos = 2; // Skip FF D8
        _logger.LogInformation("suppressing error {pos}", pos);
        while (true)
        {
            int b = await ReadByteAsync(stream);
            if (b == -1)
            {
                break;
            }

            if (b != 0xFF)
            {
                continue;
            }

            int marker = await ReadByteAsync(stream);
            if (marker == -1)
            {
                break;
            }

            if (marker == 0xC0 || marker == 0xC2) // SOF0/SOF2
            {
                await SkipAsync(stream, 3); // Length(2) + Precision(1)
                int h = await ReadBigEndianUInt16Async(stream);
                int w = await ReadBigEndianUInt16Async(stream);
                return (w, h);
            }

            // Skip other markers
            int lenHi = await ReadByteAsync(stream);
            int lenLo = await ReadByteAsync(stream);
            if (lenHi == -1 || lenLo == -1)
            {
                break;
            }

            int length = (lenHi << 8) | lenLo;
            await SkipAsync(stream, length - 2);
        }
        return null;
    }

    private async Task<int> ReadByteAsync(Stream s)
    {
        var b = new byte[1];
        return await s.ReadAsync(b, 0, 1) == 0 ? -1 : b[0];
    }

    private async Task<int> ReadBigEndianUInt16Async(Stream s)
    {
        var b = new byte[2];
        if (await s.ReadAsync(b, 0, 2) < 2)
        {
            return 0;
        }

        return (b[0] << 8) | b[1];
    }

    private async Task SkipAsync(Stream s, int count)
    {
        if (count <= 0)
        {
            return;
        }

        var b = new byte[count];
        // In highly optimized code we'd seek, but NetworkStream often doesn't support seek
        await s.ReadExactlyAsync(b, 0, count);
    }
}
