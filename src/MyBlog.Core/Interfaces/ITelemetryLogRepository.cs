using MyBlog.Core.Models;

namespace MyBlog.Core.Interfaces;

/// <summary>
/// Repository interface for telemetry log operations.
/// </summary>
public interface ITelemetryLogRepository
{
    /// <summary>Writes a log entry to the database.</summary>
    Task WriteAsync(TelemetryLog log, CancellationToken cancellationToken = default);

    /// <summary>Deletes logs older than the specified date.</summary>
    Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default);

    /// <summary>Gets recent logs for viewing.</summary>
    Task<IReadOnlyList<TelemetryLog>> GetRecentAsync(
        int count, CancellationToken cancellationToken = default);
}
