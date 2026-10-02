using System.Net;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MyBlog.Infrastructure.Data;
using MyBlog.Infrastructure.Services;
using Xunit;

namespace MyBlog.Tests.Integration;

public sealed class ImageDimensionServiceTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    public ImageDimensionServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddDbContext<BlogDbContext>(options => options.UseSqlite(_connection));
        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<BlogDbContext>().Database.EnsureCreated();
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task GetDimensionsAsync_Png_ReturnsDimensions()
    {
        var ct = TestContext.Current.CancellationToken;
        var result = await ResolveAsync(CreatePng(640, 480), ct);

        Assert.Equal(640, result?.Width);
        Assert.Equal(480, result?.Height);
    }

    [Fact]
    public async Task GetDimensionsAsync_GifWiderThanInt16_ReturnsUnsignedDimensions()
    {
        var ct = TestContext.Current.CancellationToken;
        var result = await ResolveAsync(CreateGif(40000, 300), ct);

        Assert.Equal(40000, result?.Width);
        Assert.Equal(300, result?.Height);
    }

    [Fact]
    public async Task GetDimensionsAsync_JpegLargerThanInt16_ReturnsUnsignedDimensions()
    {
        var ct = TestContext.Current.CancellationToken;
        var result = await ResolveAsync(CreateJpeg(50000, 40000, appSegmentLength: 0), ct);

        Assert.Equal(50000, result?.Width);
        Assert.Equal(40000, result?.Height);
    }

    [Fact]
    public async Task GetDimensionsAsync_JpegWithLargeAppSegment_SkipsEmbeddedFrameMarker()
    {
        var ct = TestContext.Current.CancellationToken;
        var result = await ResolveAsync(CreateJpeg(1600, 1200, appSegmentLength: 0x9000), ct);

        Assert.Equal(1600, result?.Width);
        Assert.Equal(1200, result?.Height);
    }

    [Fact]
    public async Task GetDimensionsAsync_SecondCall_UsesCacheWithoutNetwork()
    {
        var ct = TestContext.Current.CancellationToken;
        var handler = new StubHandler(CreatePng(10, 20));
        using var client = new HttpClient(handler);
        var sut = CreateService(client);
        const string url = "https://example.com/cached.png?a=1&b=2";

        var first = await sut.GetDimensionsAsync(url, ct);
        var second = await sut.GetDimensionsAsync(url, ct);

        Assert.Equal(1, handler.Calls);
        Assert.Equal(first, second);

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
        Assert.True(await db.ImageDimensionCache.AnyAsync(x => x.Url == url, ct));
    }

    [Fact]
    public async Task GetDimensionsAsync_UnknownFormat_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var result = await ResolveAsync([0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09], ct);

        Assert.Null(result);
    }

    private async Task<(int Width, int Height)?> ResolveAsync(byte[] content, CancellationToken ct)
    {
        using var client = new HttpClient(new StubHandler(content));
        var sut = CreateService(client);
        return await sut.GetDimensionsAsync($"https://example.com/{Guid.NewGuid()}", ct);
    }

    private ImageDimensionService CreateService(HttpClient client) =>
        new(client, _provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<ImageDimensionService>.Instance);

    private static byte[] CreatePng(int width, int height)
    {
        var bytes = new List<byte> { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52 };
        bytes.AddRange(BigEndian32(width));
        bytes.AddRange(BigEndian32(height));
        bytes.AddRange(new byte[8]);
        return [.. bytes];
    }

    private static byte[] CreateGif(int width, int height)
    {
        var bytes = new List<byte> { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 };
        bytes.Add((byte)(width & 0xFF));
        bytes.Add((byte)(width >> 8));
        bytes.Add((byte)(height & 0xFF));
        bytes.Add((byte)(height >> 8));
        bytes.AddRange(new byte[8]);
        return [.. bytes];
    }

    private static byte[] CreateJpeg(int width, int height, int appSegmentLength)
    {
        var bytes = new List<byte> { 0xFF, 0xD8 };

        if (appSegmentLength > 0)
        {
            bytes.AddRange([0xFF, 0xE1, (byte)(appSegmentLength >> 8), (byte)(appSegmentLength & 0xFF)]);
            var payload = new byte[appSegmentLength - 2];
            byte[] embeddedFrame = [0xFF, 0xC0, 0x00, 0x11, 0x08, 0x00, 0x10, 0x00, 0x10];
            embeddedFrame.CopyTo(payload, 0);
            bytes.AddRange(payload);
        }

        bytes.AddRange([0xFF, 0xC0, 0x00, 0x11, 0x08]);
        bytes.AddRange(BigEndian16(height));
        bytes.AddRange(BigEndian16(width));
        bytes.AddRange(new byte[12]);
        bytes.AddRange([0xFF, 0xD9]);
        return [.. bytes];
    }

    private static byte[] BigEndian32(int value) =>
        [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

    private static byte[] BigEndian16(int value) =>
        [(byte)(value >> 8), (byte)value];

    private sealed class StubHandler(byte[] content) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content)
            });
        }
    }
}
