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
    private readonly bool _isEnabled;

    // Track attempts per IP: IP -> (attempt count, window start)
    private static readonly ConcurrentDictionary<string, (int Count, DateTime WindowStart)> Attempts = new();

    // Configuration
    private const int WindowMinutes = 15;
    private const int AttemptsBeforeDelay = 5;
    private const int MaxDelaySeconds = 30;

    /// <summary>
    /// Hard cap on tracked IPs to prevent OOM from spoofed requests.
    /// When exceeded, expired entries are purged. If still over cap,
    /// new IPs are not tracked (they bypass rate limiting, which is safe
    /// since the threat model is brute-force on existing accounts, not new IPs).
    /// </summary>
    private const int MaxTrackedIps = 10_000;

    // Use this for the standard DI activation
    [ActivatorUtilitiesConstructor]
    public LoginRateLimitMiddleware(
        RequestDelegate next,
        ILogger<LoginRateLimitMiddleware> logger,
        IWebHostEnvironment environment)
        : this(next, logger, null, !environment.IsDevelopment())
    {
    }

    /// <summary>
    /// Constructor with injectable delay function for testing.
    /// </summary>
    public LoginRateLimitMiddleware(
        RequestDelegate next,
        ILogger<LoginRateLimitMiddleware> logger,
        Func<TimeSpan, CancellationToken, Task>? delayFunc)
        : this(next, logger, delayFunc, true)
    {
    }

    /// <summary>
    /// Full constructor with all options.
    /// </summary>
    private LoginRateLimitMiddleware(
        RequestDelegate next,
        ILogger<LoginRateLimitMiddleware> logger,
        Func<TimeSpan, CancellationToken, Task>? delayFunc,
        bool isEnabled)
    {
        _next = next;
        _logger = logger;
        _delayFunc = delayFunc;
        _isEnabled = isEnabled;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only rate limit POST requests to login endpoint when enabled
        if (!_isEnabled || !IsLoginPostRequest(context))
        {
            await _next(context);
            return;
        }

        var ip = GetClientIp(context);
        // Record the attempt FIRST, then calculate delay based on the new count
        RecordAttempt(ip);
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
    }

    private static bool IsLoginPostRequest(HttpContext context)
    {
        return context.Request.Method == HttpMethods.Post &&
               context.Request.Path.StartsWithSegments("/account/login", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the client IP from the connection. Does NOT manually parse X-Forwarded-For.
    /// If running behind a reverse proxy, configure ASP.NET Core's ForwardedHeaders middleware
    /// which safely sets RemoteIpAddress from trusted proxies only.
    /// </summary>
    private static string GetClientIp(HttpContext context)
    {
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    /// <summary>
    /// Calculate delay based on attempt count. Exposed for unit testing.
    /// </summary>
    public static TimeSpan CalculateDelay(string ip)
    {
        if (!Attempts.TryGetValue(ip, out var record))
        {
            return TimeSpan.Zero;
        }

        // Check if window has expired
        if (DateTime.UtcNow - record.WindowStart > TimeSpan.FromMinutes(WindowMinutes))
        {
            Attempts.TryRemove(ip, out _);
            return TimeSpan.Zero;
        }

        if (record.Count <= AttemptsBeforeDelay)
        {
            return TimeSpan.Zero;
        }

        // Exponential backoff: 1s, 2s, 4s, 8s, 16s, then cap at 30s
        var delayMultiplier = record.Count - AttemptsBeforeDelay;
        var delaySeconds = Math.Min(Math.Pow(2, delayMultiplier - 1), MaxDelaySeconds);
        return TimeSpan.FromSeconds(delaySeconds);
    }

    private static void RecordAttempt(string ip)
    {
        // Enforce hard cap to prevent OOM from distributed/spoofed sources
        if (Attempts.Count >= MaxTrackedIps && !Attempts.ContainsKey(ip))
        {
            CleanupExpiredEntries();

            // If still at capacity after cleanup, skip tracking this IP.
            // This is safe: untracked IPs simply bypass rate limiting,
            // which is acceptable since legitimate users are already tracked.
            if (Attempts.Count >= MaxTrackedIps)
            {
                return;
            }
        }

        Attempts.AddOrUpdate(
            ip,
            _ => (1, DateTime.UtcNow),
            (_, existing) =>
            {
                // Reset if window expired
                if (DateTime.UtcNow - existing.WindowStart > TimeSpan.FromMinutes(WindowMinutes))
                {
                    return (1, DateTime.UtcNow);
                }
                return (existing.Count + 1, existing.WindowStart);
            });
    }

    /// <summary>
    /// Removes all entries whose tracking window has expired.
    /// </summary>
    private static void CleanupExpiredEntries()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-WindowMinutes);
        foreach (var kvp in Attempts)
        {
            if (kvp.Value.WindowStart < cutoff)
            {
                Attempts.TryRemove(kvp.Key, out _);
            }
        }
    }

    /// <summary>
    /// Clear all attempt records. Used for testing.
    /// </summary>
    public static void ClearAttempts()
    {
        Attempts.Clear();
    }
}

/// <summary>
/// Extension methods for login rate limiting.
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
