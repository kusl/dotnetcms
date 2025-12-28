using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyBlog.Core.Interfaces;

namespace MyBlog.Infrastructure.Services;

/// <summary>
/// Background service that cleans up old telemetry logs.
/// </summary>
public sealed class TelemetryCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TelemetryCleanupService> _logger;
    private readonly int _retentionDays;

    /// <summary>Initializes a new instance of TelemetryCleanupService.</summary>
    public TelemetryCleanupService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<TelemetryCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _retentionDays = configuration.GetValue("Telemetry:RetentionDays", 30);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run cleanup immediately on startup
        await CleanupAsync(stoppingToken);

        // Then run daily
        using var timer = new PeriodicTimer(TimeSpan.FromDays(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CleanupAsync(stoppingToken);
        }
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ITelemetryLogRepository>();

            var cutoff = DateTime.UtcNow.AddDays(-_retentionDays);
            var deleted = await repository.DeleteOlderThanAsync(cutoff, cancellationToken);

            if (deleted > 0)
            {
                _logger.LogInformation(
                    "Telemetry cleanup: deleted {Count} logs older than {Days} days",
                    deleted, _retentionDays);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during telemetry cleanup");
        }
    }
}
