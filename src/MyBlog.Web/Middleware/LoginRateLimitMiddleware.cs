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
    private static readonly ConcurrentDictionary<string, (int Count, DateTime WindowStart)> Attempts = new();
    // Configuration
    private const int WindowMinutes = 15;
    private const int AttemptsBeforeDelay = 5;
    private const int MaxDelaySeconds = 30;

    // Use this for the standard DI activation
    [ActivatorUtilitiesConstructor]
    public LoginRateLimitMiddleware(RequestDelegate next, ILogger<LoginRateLimitMiddleware> logger)
        : this(next, logger, null)
    {
    }

    /// <summary>
    /// Constructor with injectable delay function for testing.
    /// </summary>
    public LoginRateLimitMiddleware(
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
               context.Request.Path.StartsWithSegments("/account/login", StringComparison.OrdinalIgnoreCase);
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
    public static TimeSpan CalculateDelay(string ip)
    {
        if (!Attempts.TryGetValue(ip, out var record))
        {
            return TimeSpan.Zero;
        }

        // Reset if window expired
        if (DateTime.UtcNow - record.WindowStart > TimeSpan.FromMinutes(WindowMinutes))
        {
            Attempts.TryRemove(ip, out _);
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
    /// Records a login attempt for the given IP.
    /// Exposed for testing.
    /// </summary>
    internal static void RecordAttempt(string ip)
    {
        var now = DateTime.UtcNow;
        Attempts.AddOrUpdate(
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
    /// Clears all tracked attempts.
    /// For testing only.
    /// </summary>
    public static void ClearAttempts()
    {
        Attempts.Clear();
    }

    private static void CleanupOldEntries()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-WindowMinutes * 2);
        foreach (var kvp in Attempts)
        {
            if (kvp.Value.WindowStart < cutoff)
            {
                Attempts.TryRemove(kvp.Key, out _);
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
