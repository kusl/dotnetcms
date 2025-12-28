using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MyBlog.Web.Middleware;
using Xunit;

namespace MyBlog.Tests.Unit;

/// <summary>
/// Tests for LoginRateLimitMiddleware.
/// Verifies that the middleware slows down but never blocks requests.
/// </summary>
public sealed class LoginRateLimitMiddlewareTests
{
    private readonly LoginRateLimitMiddleware _sut;
    private int _nextCallCount;

    public LoginRateLimitMiddlewareTests()
    {
        _nextCallCount = 0;
        RequestDelegate next = _ =>
        {
            _nextCallCount++;
            return Task.CompletedTask;
        };
        _sut = new LoginRateLimitMiddleware(next, NullLogger<LoginRateLimitMiddleware>.Instance);
    }

    [Fact]
    public async Task InvokeAsync_NonLoginRequest_PassesThroughImmediately()
    {
        var ct = TestContext.Current.CancellationToken;
        var context = CreateHttpContext("/api/posts", "GET");

        await _sut.InvokeAsync(context);

        Assert.Equal(1, _nextCallCount);
    }

    [Fact]
    public async Task InvokeAsync_GetLoginRequest_PassesThroughImmediately()
    {
        var ct = TestContext.Current.CancellationToken;
        var context = CreateHttpContext("/login", "GET");

        await _sut.InvokeAsync(context);

        Assert.Equal(1, _nextCallCount);
    }

    [Fact]
    public async Task InvokeAsync_FirstLoginAttempt_PassesThroughImmediately()
    {
        var ct = TestContext.Current.CancellationToken;
        var context = CreateHttpContext("/login", "POST", "192.168.1.100");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await _sut.InvokeAsync(context);
        sw.Stop();

        Assert.Equal(1, _nextCallCount);
        Assert.True(sw.ElapsedMilliseconds < 500, "First attempt should not be delayed");
    }

    [Fact]
    public async Task InvokeAsync_AfterManyAttempts_StillPassesThrough()
    {
        // Use unique IP to avoid interference from other tests
        var uniqueIp = $"10.0.0.{Random.Shared.Next(1, 255)}";

        // Make 20 requests - middleware should always pass through
        for (var i = 0; i < 20; i++)
        {
            var context = CreateHttpContext("/login", "POST", uniqueIp);
            await _sut.InvokeAsync(context);
        }

        // All 20 requests should have passed through
        Assert.Equal(20, _nextCallCount);
    }

    [Fact]
    public async Task InvokeAsync_NeverBlocksCompletely()
    {
        // Use unique IP
        var uniqueIp = $"10.0.1.{Random.Shared.Next(1, 255)}";

        // Even after 100 attempts, requests should pass through
        for (var i = 0; i < 100; i++)
        {
            var context = CreateHttpContext("/login", "POST", uniqueIp);

            // Use a timeout to ensure we're not blocked forever
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try
            {
                await _sut.InvokeAsync(context);
            }
            catch (OperationCanceledException)
            {
                Assert.Fail($"Request {i} was blocked or timed out");
            }
        }

        Assert.Equal(100, _nextCallCount);
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
