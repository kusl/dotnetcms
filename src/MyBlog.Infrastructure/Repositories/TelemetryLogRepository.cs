using Microsoft.EntityFrameworkCore;
using MyBlog.Core.Interfaces;
using MyBlog.Infrastructure.Data;

namespace MyBlog.Infrastructure.Repositories;

public sealed class TelemetryLogRepository : ITelemetryLogRepository
{
    private readonly BlogDbContext _context;

    public TelemetryLogRepository(BlogDbContext context)
    {
        _context = context;
    }

    public async Task<int> DeleteOlderThanAsync(
        DateTime cutoffUtc, CancellationToken cancellationToken = default) =>
        await _context.TelemetryLogs
            .Where(l => l.TimestampUtc < cutoffUtc)
            .ExecuteDeleteAsync(cancellationToken);

    public async Task<int> TrimToNewestAsync(int maxRows, CancellationToken cancellationToken = default)
    {
        var threshold = await _context.TelemetryLogs
            .OrderByDescending(l => l.TimestampUtc)
            .Skip(maxRows)
            .Select(l => (DateTime?)l.TimestampUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (threshold is null)
        {
            return 0;
        }

        return await _context.TelemetryLogs
            .Where(l => l.TimestampUtc <= threshold.Value)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
