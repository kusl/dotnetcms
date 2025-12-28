using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using MyBlog.Web.Middleware;

namespace MyBlog.Tests.Unit;

/// <summary>
/// Tests for LoginRateLimitMiddleware.
/// Verifies that the middleware slows down but never blocks requests.
/// </summary>
public sealed class LoginRateLimitMiddlewareTests : IDisposable
{
    private readonly LoginRateLimitMiddleware _sut;
    private int _nextCallCount;
    private readonly List<TimeSpan> _recordedDelays = [];

    public LoginRateLimitMiddlewareTests()
    {
        // Clear any state from previous tests
        LoginRateLimitMiddleware.ClearAttempts();

        _nextCallCount = 0;
        RequestDelegate next = _ =>
        {
            _nextCallCount++;
            return Task.CompletedTask;
        };

        // Use a no-op delay function that just records the delay
        // This makes tests fast while still verifying delay logic
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

    public void Dispose()
    {
        // Clean up after each test
        LoginRateLimitMiddleware.ClearAttempts();
    }

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

        // First 5 attempts should have no delay
        for (var i = 0; i < 5; i++)
        {
            var context = CreateHttpContext("/login", "POST", uniqueIp);
            await _sut.InvokeAsync(context);
        }

        Assert.Equal(5, _nextCallCount);
        Assert.Empty(_recordedDelays); // No delays for first 5 attempts
    }

    [Fact]
    public async Task InvokeAsync_SixthAttempt_HasOneSecondDelay()
    {
        var uniqueIp = $"192.168.{Random.Shared.Next(1, 255)}.{Random.Shared.Next(1, 255)}";

        // Make 6 attempts
        for (var i = 0; i < 6; i++)
        {
            var context = CreateHttpContext("/login", "POST", uniqueIp);
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

        // Make 10 attempts: 5 no-delay + 5 with delays
        for (var i = 0; i < 10; i++)
        {
            var context = CreateHttpContext("/login", "POST", uniqueIp);
            await _sut.InvokeAsync(context);
        }

        Assert.Equal(10, _nextCallCount);
        Assert.Equal(5, _recordedDelays.Count); // Delays start after attempt 5

        // Verify exponential progression: 1s, 2s, 4s, 8s, 16s
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

        // Make enough attempts to hit the cap (5 no-delay + enough to exceed 30s)
        // After attempt 5: 1, 2, 4, 8, 16, 30, 30, 30...
        for (var i = 0; i < 15; i++)
        {
            var context = CreateHttpContext("/login", "POST", uniqueIp);
            await _sut.InvokeAsync(context);
        }

        Assert.Equal(15, _nextCallCount);

        // Verify cap at 30 seconds (attempts 11+ should all be 30s)
        var maxDelays = _recordedDelays.Where(d => d == TimeSpan.FromSeconds(30)).ToList();
        Assert.True(maxDelays.Count >= 4, "Should have multiple 30-second delays");
        Assert.True(_recordedDelays.All(d => d <= TimeSpan.FromSeconds(30)), "No delay should exceed 30 seconds");
    }

    [Fact]
    public async Task InvokeAsync_AfterManyAttempts_NeverBlocks()
    {
        var uniqueIp = $"10.0.{Random.Shared.Next(1, 255)}.{Random.Shared.Next(1, 255)}";

        // Make 100 attempts - should all pass through (with delays, but never blocked)
        for (var i = 0; i < 100; i++)
        {
            var context = CreateHttpContext("/login", "POST", uniqueIp);
            await _sut.InvokeAsync(context);
        }

        // Key assertion: ALL requests passed through, none were blocked
        Assert.Equal(100, _nextCallCount);
    }

    [Fact]
    public async Task InvokeAsync_DifferentIPs_IndependentTracking()
    {
        var ip1 = $"10.1.{Random.Shared.Next(1, 255)}.{Random.Shared.Next(1, 255)}";
        var ip2 = $"10.2.{Random.Shared.Next(1, 255)}.{Random.Shared.Next(1, 255)}";

        // 6 attempts from IP1 (should trigger delay on 6th)
        for (var i = 0; i < 6; i++)
        {
            var context = CreateHttpContext("/login", "POST", ip1);
            await _sut.InvokeAsync(context);
        }

        var ip1Delays = _recordedDelays.Count;
        Assert.Equal(1, ip1Delays); // One delay after 5th attempt

        // First attempt from IP2 should have no delay
        var context2 = CreateHttpContext("/login", "POST", ip2);
        await _sut.InvokeAsync(context2);

        // No new delays should have been added for IP2
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
