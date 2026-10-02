using System.Collections.Concurrent;

namespace MyBlog.Web.Middleware;

public sealed class LoginRateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<LoginRateLimitMiddleware> _logger;
    private readonly Func<TimeSpan, CancellationToken, Task>? _delayFunc;
    private readonly bool _isEnabled;

    private static readonly ConcurrentDictionary<string, (int Count, DateTime WindowStart)> Attempts = new();

    private const int WindowMinutes = 15;
    private const int AttemptsBeforeDelay = 5;
    private const int MaxDelaySeconds = 30;

    private const int MaxTrackedIps = 10_000;

    public LoginRateLimitMiddleware(
        RequestDelegate next,
        ILogger<LoginRateLimitMiddleware> logger,
        IWebHostEnvironment environment)
        : this(next, logger, null, !environment.IsDevelopment())
    {
    }

    public LoginRateLimitMiddleware(
        RequestDelegate next,
        ILogger<LoginRateLimitMiddleware> logger,
        Func<TimeSpan, CancellationToken, Task>? delayFunc)
        : this(next, logger, delayFunc, true)
    {
    }

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
        if (!_isEnabled || !IsLoginPostRequest(context))
        {
            await _next(context);
            return;
        }

        var ip = GetClientIp(context);
        RecordAttempt(ip);
        var delay = CalculateDelay(ip);
        if (delay > TimeSpan.Zero)
        {
            _logger.LogInformation(
                "Rate limiting login attempt from {IP}, delaying {Seconds}s",
                ip, delay.TotalSeconds);
            if (_delayFunc != null)
            {
                await _delayFunc(delay, context.RequestAborted);
            }
            else
            {
                await Task.Delay(delay, context.RequestAborted);
            }
        }

        await _next(context);
    }

    private static bool IsLoginPostRequest(HttpContext context) =>
        context.Request.Method == HttpMethods.Post &&
        context.Request.Path.StartsWithSegments("/account/login", StringComparison.OrdinalIgnoreCase);

    private static string GetClientIp(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    public static TimeSpan CalculateDelay(string ip)
    {
        if (!Attempts.TryGetValue(ip, out var record))
        {
            return TimeSpan.Zero;
        }

        if (DateTime.UtcNow - record.WindowStart > TimeSpan.FromMinutes(WindowMinutes))
        {
            Attempts.TryRemove(ip, out _);
            return TimeSpan.Zero;
        }

        if (record.Count <= AttemptsBeforeDelay)
        {
            return TimeSpan.Zero;
        }

        var delayMultiplier = record.Count - AttemptsBeforeDelay;
        var delaySeconds = Math.Min(Math.Pow(2, delayMultiplier - 1), MaxDelaySeconds);
        return TimeSpan.FromSeconds(delaySeconds);
    }

    private static void RecordAttempt(string ip)
    {
        if (Attempts.Count >= MaxTrackedIps && !Attempts.ContainsKey(ip))
        {
            CleanupExpiredEntries();

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
                if (DateTime.UtcNow - existing.WindowStart > TimeSpan.FromMinutes(WindowMinutes))
                {
                    return (1, DateTime.UtcNow);
                }
                return (existing.Count + 1, existing.WindowStart);
            });
    }

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

    public static void ClearAttempts() => Attempts.Clear();
}

public static class LoginRateLimitMiddlewareExtensions
{
    public static IApplicationBuilder UseLoginRateLimit(this IApplicationBuilder app)
    {
        var logger = app.ApplicationServices.GetRequiredService<ILogger<LoginRateLimitMiddleware>>();
        var environment = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();
        return app.Use(next => new LoginRateLimitMiddleware(next, logger, environment).InvokeAsync);
    }
}
