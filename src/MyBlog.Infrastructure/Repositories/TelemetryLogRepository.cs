using Microsoft.EntityFrameworkCore;
using MyBlog.Core.Interfaces;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;

namespace MyBlog.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of the telemetry log repository.
/// </summary>
public sealed class TelemetryLogRepository : ITelemetryLogRepository
{
    private readonly BlogDbContext _context;

    /// <summary>Initializes a new instance of TelemetryLogRepository.</summary>
    public TelemetryLogRepository(BlogDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task WriteAsync(TelemetryLog log, CancellationToken cancellationToken = default)
    {
        _context.TelemetryLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> DeleteOlderThanAsync(
        DateTime cutoffUtc, CancellationToken cancellationToken = default)
    {
        return await _context.TelemetryLogs
            .Where(l => l.TimestampUtc < cutoffUtc)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TelemetryLog>> GetRecentAsync(
        int count, CancellationToken cancellationToken = default)
    {
        return await _context.TelemetryLogs
            .AsNoTracking()
            .OrderByDescending(l => l.TimestampUtc)
            .Take(count)
            .ToListAsync(cancellationToken);
    }
}
