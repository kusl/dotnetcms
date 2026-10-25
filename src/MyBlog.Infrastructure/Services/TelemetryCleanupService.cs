using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyBlog.Core.Interfaces;
using MyBlog.Infrastructure.Telemetry;

namespace MyBlog.Infrastructure.Services;

public sealed class TelemetryCleanupService : BackgroundService
{
    internal const int MaxRetentionDays = 30;

    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TelemetryCleanupService> _logger;
    private readonly int _retentionDays;
    private readonly int _maxDatabaseRows;
    private readonly long _maxLogFileBytes;
    private readonly string? _logsDirectory;

    public TelemetryCleanupService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<TelemetryCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _retentionDays = Math.Clamp(configuration.GetValue("Telemetry:RetentionDays", MaxRetentionDays), 1, MaxRetentionDays);
        _maxDatabaseRows = Math.Max(1, configuration.GetValue("Telemetry:MaxDatabaseRows", 50_000));
        _maxLogFileBytes = Math.Max(1, configuration.GetValue("Telemetry:MaxLogFileMegabytes", 100)) * 1024L * 1024L;
        _logsDirectory = TelemetryPathResolver.GetLogsDirectory();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CleanupAsync(stoppingToken);

        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CleanupAsync(stoppingToken);
        }
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddDays(-_retentionDays);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ITelemetryLogRepository>();

            var expired = await repository.DeleteOlderThanAsync(cutoff, cancellationToken);
            var trimmed = await repository.TrimToNewestAsync(_maxDatabaseRows, cancellationToken);

            if (expired + trimmed > 0)
            {
                _logger.LogInformation(
                    "Telemetry cleanup: deleted {Expired} database logs older than {Days} days and {Trimmed} beyond {MaxRows} rows",
                    expired, _retentionDays, trimmed, _maxDatabaseRows);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Telemetry cleanup failed for database logs");
        }

        if (_logsDirectory is null)
        {
            return;
        }

        try
        {
            var (deleted, failed) = LogFileRetention.Apply(_logsDirectory, cutoff, _maxLogFileBytes);

            if (deleted > 0)
            {
                _logger.LogInformation(
                    "Telemetry cleanup: deleted {Count} log files from {Directory}", deleted, _logsDirectory);
            }

            if (failed > 0)
            {
                _logger.LogWarning(
                    "Telemetry cleanup: could not delete {Count} log files in {Directory}", failed, _logsDirectory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telemetry cleanup failed for log files in {Directory}", _logsDirectory);
        }
    }
}
