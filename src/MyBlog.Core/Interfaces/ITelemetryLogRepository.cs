namespace MyBlog.Core.Interfaces;

public interface ITelemetryLogRepository
{
    Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default);

    Task<int> TrimToNewestAsync(int maxRows, CancellationToken cancellationToken = default);
}
