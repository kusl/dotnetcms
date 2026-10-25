using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using MyBlog.Web.Middleware;
using Xunit;

namespace MyBlog.Tests.Unit;

public sealed class LoginRateLimitMiddlewareTests : IDisposable
{
    private readonly LoginRateLimitMiddleware _sut;
    private int _nextCallCount;
    private readonly List<TimeSpan> _recordedDelays = [];

    public LoginRateLimitMiddlewareTests()
    {
        LoginRateLimitMiddleware.ClearAttempts();
        _nextCallCount = 0;
        RequestDelegate next = _ =>
        {
            _nextCallCount++;
            return Task.CompletedTask;
        };

        Task NoOpDelay(TimeSpan delay, CancellationToken ct)
        {
            _recordedDelays.Add(delay);
            return Task.CompletedTask;
        }

        _sut = new LoginRateLimitMiddleware(
            next,
            NullLogger<LoginRateLimitMiddleware>.Instance,
            NoOpDelay);
    }

    public void Dispose() =>
        LoginRateLimitMiddleware.ClearAttempts();

    [Fact]
    public async Task InvokeAsync_NonLoginRequest_PassesThroughImmediately()
    {
        var context = CreateHttpContext("/api/posts", "GET");
        await _sut.InvokeAsync(context);

        Assert.Equal(1, _nextCallCount);
        Assert.Empty(_recordedDelays);
    }

    [Fact]
    public async Task InvokeAsync_GetLoginRequest_PassesThroughImmediately()
    {
        var context = CreateHttpContext("/login", "GET");
        await _sut.InvokeAsync(context);

        Assert.Equal(1, _nextCallCount);
        Assert.Empty(_recordedDelays);
    }

    [Fact]
    public async Task InvokeAsync_FirstFiveAttempts_NoDelay()
    {
        var uniqueIp = $"192.168.{Random.Shared.Next(1, 255)}.{Random.Shared.Next(1, 255)}";
        for (var i = 0; i < 5; i++)
        {
            var context = CreateHttpContext("/account/login", "POST", uniqueIp);
            await _sut.InvokeAsync(context);
        }

        Assert.Equal(5, _nextCallCount);
        Assert.Empty(_recordedDelays);
    }

    [Fact]
    public async Task InvokeAsync_SixthAttempt_HasOneSecondDelay()
    {
        var uniqueIp = $"192.168.{Random.Shared.Next(1, 255)}.{Random.Shared.Next(1, 255)}";
        for (var i = 0; i < 6; i++)
        {
            var context = CreateHttpContext("/account/login", "POST", uniqueIp);
            await _sut.InvokeAsync(context);
        }

        Assert.Equal(6, _nextCallCount);
        Assert.Single(_recordedDelays);
        Assert.Equal(TimeSpan.FromSeconds(1), _recordedDelays[0]);
    }

    [Fact]
    public async Task InvokeAsync_ProgressiveDelays_IncreaseExponentially()
    {
        var uniqueIp = $"192.168.{Random.Shared.Next(1, 255)}.{Random.Shared.Next(1, 255)}";
        for (var i = 0; i < 10; i++)
        {
            var context = CreateHttpContext("/account/login", "POST", uniqueIp);
            await _sut.InvokeAsync(context);
        }

        Assert.Equal(10, _nextCallCount);
        Assert.Equal(5, _recordedDelays.Count);

        Assert.Equal(TimeSpan.FromSeconds(1), _recordedDelays[0]);
        Assert.Equal(TimeSpan.FromSeconds(2), _recordedDelays[1]);
        Assert.Equal(TimeSpan.FromSeconds(4), _recordedDelays[2]);
        Assert.Equal(TimeSpan.FromSeconds(8), _recordedDelays[3]);
        Assert.Equal(TimeSpan.FromSeconds(16), _recordedDelays[4]);
    }

    [Fact]
    public async Task InvokeAsync_DelayCappedAt30Seconds()
    {
        var uniqueIp = $"192.168.{Random.Shared.Next(1, 255)}.{Random.Shared.Next(1, 255)}";
        for (var i = 0; i < 15; i++)
        {
            var context = CreateHttpContext("/account/login", "POST", uniqueIp);
            await _sut.InvokeAsync(context);
        }

        Assert.Equal(15, _nextCallCount);
        var maxDelays = _recordedDelays.Where(d => d == TimeSpan.FromSeconds(30)).ToList();
        Assert.True(maxDelays.Count >= 4, "Should have multiple 30-second delays");
        Assert.True(_recordedDelays.All(d => d <= TimeSpan.FromSeconds(30)), "No delay should exceed 30 seconds");
    }

    [Fact]
    public async Task InvokeAsync_AfterManyAttempts_NeverBlocks()
    {
        var uniqueIp = $"10.0.{Random.Shared.Next(1, 255)}.{Random.Shared.Next(1, 255)}";
        for (var i = 0; i < 100; i++)
        {
            var context = CreateHttpContext("/account/login", "POST", uniqueIp);
            await _sut.InvokeAsync(context);
        }

        Assert.Equal(100, _nextCallCount);
    }

    [Fact]
    public async Task InvokeAsync_DifferentIPs_IndependentTracking()
    {
        var ip1 = $"10.1.{Random.Shared.Next(1, 255)}.{Random.Shared.Next(1, 255)}";
        var ip2 = $"10.2.{Random.Shared.Next(1, 255)}.{Random.Shared.Next(1, 255)}";

        for (var i = 0; i < 6; i++)
        {
            var context = CreateHttpContext("/account/login", "POST", ip1);
            await _sut.InvokeAsync(context);
        }

        var ip1Delays = _recordedDelays.Count;
        Assert.Equal(1, ip1Delays);

        var context2 = CreateHttpContext("/account/login", "POST", ip2);
        await _sut.InvokeAsync(context2);

        Assert.Equal(ip1Delays, _recordedDelays.Count);
    }

    [Fact]
    public void CalculateDelay_UnknownIP_ReturnsZero()
    {
        var delay = LoginRateLimitMiddleware.CalculateDelay("unknown-ip-never-seen");
        Assert.Equal(TimeSpan.Zero, delay);
    }

    private static DefaultHttpContext CreateHttpContext(string path, string method, string? remoteIp = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = method;

        if (remoteIp != null)
        {
            context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(remoteIp);
        }

        return context;
    }
}
