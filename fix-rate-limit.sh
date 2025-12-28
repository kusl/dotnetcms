#!/bin/bash
# =============================================================================
# Fix Rate Limit Middleware Tests
# =============================================================================
# The tests are slow because they actually wait for real Task.Delay calls.
# Solution: Make the delay mechanism injectable so tests can skip the waits.
# =============================================================================

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SRC_DIR="$SCRIPT_DIR/src"

echo "=============================================="
echo "  Fixing Rate Limit Middleware Tests"
echo "=============================================="
echo ""

# =============================================================================
# Step 1: Replace the middleware with a testable version
# =============================================================================
echo "[1/2] Updating LoginRateLimitMiddleware to be testable..."

cat << 'EOF' > "$SRC_DIR/MyBlog.Web/Middleware/LoginRateLimitMiddleware.cs"
using System.Collections.Concurrent;

namespace MyBlog.Web.Middleware;

/// <summary>
/// Rate limiting middleware for login attempts.
/// Slows down requests but NEVER blocks users completely.
/// </summary>
public sealed class LoginRateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<LoginRateLimitMiddleware> _logger;
    private readonly Func<TimeSpan, CancellationToken, Task>? _delayFunc;

    // Track attempts per IP: IP -> (attempt count, window start)
    private static readonly ConcurrentDictionary<string, (int Count, DateTime WindowStart)> _attempts = new();

    // Configuration
    private const int WindowMinutes = 15;
    private const int AttemptsBeforeDelay = 5;
    private const int MaxDelaySeconds = 30;

    public LoginRateLimitMiddleware(RequestDelegate next, ILogger<LoginRateLimitMiddleware> logger)
        : this(next, logger, null)
    {
    }

    /// <summary>
    /// Constructor with injectable delay function for testing.
    /// </summary>
    internal LoginRateLimitMiddleware(
        RequestDelegate next,
        ILogger<LoginRateLimitMiddleware> logger,
        Func<TimeSpan, CancellationToken, Task>? delayFunc)
    {
        _next = next;
        _logger = logger;
        _delayFunc = delayFunc;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only rate limit POST requests to login endpoint
        if (!IsLoginPostRequest(context))
        {
            await _next(context);
            return;
        }

        var ip = GetClientIp(context);
        var delay = CalculateDelay(ip);

        if (delay > TimeSpan.Zero)
        {
            _logger.LogInformation(
                "Rate limiting login attempt from {IP}, delaying {Seconds}s",
                ip, delay.TotalSeconds);

            // Use injected delay function if available (for testing), otherwise real delay
            if (_delayFunc != null)
            {
                await _delayFunc(delay, context.RequestAborted);
            }
            else
            {
                await Task.Delay(delay, context.RequestAborted);
            }
        }

        // Always proceed - never block
        await _next(context);

        // Record the attempt after processing
        RecordAttempt(ip);
    }

    private static bool IsLoginPostRequest(HttpContext context)
    {
        return context.Request.Method == HttpMethods.Post &&
               context.Request.Path.StartsWithSegments("/login", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetClientIp(HttpContext context)
    {
        // Check for forwarded IP (behind proxy/load balancer)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            var ip = forwardedFor.Split(',')[0].Trim();
            if (!string.IsNullOrEmpty(ip))
            {
                return ip;
            }
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    /// <summary>
    /// Calculates the delay for a given IP. Exposed for testing.
    /// </summary>
    internal static TimeSpan CalculateDelay(string ip)
    {
        if (!_attempts.TryGetValue(ip, out var record))
        {
            return TimeSpan.Zero;
        }

        // Reset if window expired
        if (DateTime.UtcNow - record.WindowStart > TimeSpan.FromMinutes(WindowMinutes))
        {
            _attempts.TryRemove(ip, out _);
            return TimeSpan.Zero;
        }

        // No delay for first few attempts
        if (record.Count < AttemptsBeforeDelay)
        {
            return TimeSpan.Zero;
        }

        // Progressive delay: 1s, 2s, 4s, 8s, ... capped at MaxDelaySeconds
        var delayMultiplier = record.Count - AttemptsBeforeDelay;
        var delaySeconds = Math.Min(Math.Pow(2, delayMultiplier), MaxDelaySeconds);
        return TimeSpan.FromSeconds(delaySeconds);
    }

    /// <summary>
    /// Records a login attempt for the given IP. Exposed for testing.
    /// </summary>
    internal static void RecordAttempt(string ip)
    {
        var now = DateTime.UtcNow;

        _attempts.AddOrUpdate(
            ip,
            _ => (1, now),
            (_, existing) =>
            {
                // Reset window if expired
                if (now - existing.WindowStart > TimeSpan.FromMinutes(WindowMinutes))
                {
                    return (1, now);
                }
                return (existing.Count + 1, existing.WindowStart);
            });

        // Cleanup old entries periodically (every 100th request)
        if (Random.Shared.Next(100) == 0)
        {
            CleanupOldEntries();
        }
    }

    /// <summary>
    /// Clears all tracked attempts. For testing only.
    /// </summary>
    internal static void ClearAttempts()
    {
        _attempts.Clear();
    }

    private static void CleanupOldEntries()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-WindowMinutes * 2);
        foreach (var kvp in _attempts)
        {
            if (kvp.Value.WindowStart < cutoff)
            {
                _attempts.TryRemove(kvp.Key, out _);
            }
        }
    }
}

/// <summary>
/// Extension methods for LoginRateLimitMiddleware.
/// </summary>
public static class LoginRateLimitMiddlewareExtensions
{
    /// <summary>
    /// Adds login rate limiting middleware that slows down repeated attempts
    /// but never completely blocks users.
    /// </summary>
    public static IApplicationBuilder UseLoginRateLimit(this IApplicationBuilder app)
    {
        return app.UseMiddleware<LoginRateLimitMiddleware>();
    }
}
EOF

echo "      Done."

# =============================================================================
# Step 2: Update the tests to use no-op delay
# =============================================================================
echo "[2/2] Updating tests to skip actual delays..."

cat << 'EOF' > "$SRC_DIR/MyBlog.Tests/Unit/LoginRateLimitMiddlewareTests.cs"
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
EOF

echo "      Done."

echo ""
echo "=============================================="
echo "  Fix Complete!"
echo "=============================================="
echo ""
echo "Changes made:"
echo ""
echo "  1. Updated LoginRateLimitMiddleware:"
echo "     - Added injectable delay function (internal constructor)"
echo "     - Added ClearAttempts() method for test isolation"
echo "     - Made CalculateDelay() internal for direct testing"
echo ""
echo "  2. Rewrote LoginRateLimitMiddlewareTests:"
echo "     - Uses no-op delay function (records delays but doesn't wait)"
echo "     - Tests run in milliseconds instead of minutes"
echo "     - Added IDisposable to clean up state between tests"
echo "     - Added more specific tests for delay progression"
echo ""
echo "The key insight: the old tests were actually calling Task.Delay()"
echo "with real delays (1s, 2s, 4s, 8s... 30s) for 100 iterations."
echo "That adds up to ~45+ minutes of waiting!"
echo ""
echo "Next steps:"
echo "  1. Run: chmod +x fix-rate-limit-tests.sh && ./fix-rate-limit-tests.sh"
echo "  2. Run: cd src && dotnet test"
echo ""
