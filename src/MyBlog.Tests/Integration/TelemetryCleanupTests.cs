using Microsoft.EntityFrameworkCore;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;
using MyBlog.Infrastructure.Repositories;
using MyBlog.Infrastructure.Telemetry;
using Xunit;

namespace MyBlog.Tests.Integration;

public sealed class TelemetryCleanupTests : IAsyncDisposable
{
    private readonly BlogDbContext _context;
    private readonly TelemetryLogRepository _sut;
    private readonly string _logsDirectory;

    public TelemetryCleanupTests()
    {
        var options = new DbContextOptionsBuilder<BlogDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _context = new BlogDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _sut = new TelemetryLogRepository(_context);

        _logsDirectory = Path.Combine(Path.GetTempPath(), $"myblog-logs-{Guid.NewGuid()}");
        Directory.CreateDirectory(_logsDirectory);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        Directory.Delete(_logsDirectory, recursive: true);
    }

    [Fact]
    public async Task DeleteOlderThanAsync_RemovesOldLogs()
    {
        var ct = TestContext.Current.CancellationToken;
        await AddLogAsync(DateTime.UtcNow.AddDays(-60), ct);
        await AddLogAsync(DateTime.UtcNow.AddDays(-5), ct);

        var deleted = await _sut.DeleteOlderThanAsync(DateTime.UtcNow.AddDays(-30), ct);

        Assert.Equal(1, deleted);
        Assert.Equal(1, await _context.TelemetryLogs.CountAsync(ct));
    }

    [Fact]
    public async Task DeleteOlderThanAsync_WithNoOldLogs_ReturnsZero()
    {
        var ct = TestContext.Current.CancellationToken;
        await AddLogAsync(DateTime.UtcNow.AddDays(-5), ct);

        var deleted = await _sut.DeleteOlderThanAsync(DateTime.UtcNow.AddDays(-30), ct);

        Assert.Equal(0, deleted);
    }

    [Fact]
    public async Task DeleteOlderThanAsync_WithEmptyTable_ReturnsZero()
    {
        var ct = TestContext.Current.CancellationToken;

        var deleted = await _sut.DeleteOlderThanAsync(DateTime.UtcNow.AddDays(-30), ct);

        Assert.Equal(0, deleted);
    }

    [Fact]
    public async Task TrimToNewestAsync_KeepsOnlyNewestRows()
    {
        var ct = TestContext.Current.CancellationToken;
        var now = DateTime.UtcNow;
        for (var i = 0; i < 5; i++)
        {
            await AddLogAsync(now.AddMinutes(-i), ct);
        }

        var deleted = await _sut.TrimToNewestAsync(3, ct);

        Assert.Equal(2, deleted);
        var remaining = await _context.TelemetryLogs.AsNoTracking()
            .OrderByDescending(l => l.TimestampUtc)
            .Select(l => l.TimestampUtc)
            .ToListAsync(ct);
        Assert.Equal(new[] { now, now.AddMinutes(-1), now.AddMinutes(-2) }, remaining);
    }

    [Fact]
    public async Task TrimToNewestAsync_UnderLimit_DeletesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await AddLogAsync(DateTime.UtcNow, ct);

        var deleted = await _sut.TrimToNewestAsync(10, ct);

        Assert.Equal(0, deleted);
        Assert.Equal(1, await _context.TelemetryLogs.CountAsync(ct));
    }

    [Fact]
    public void LogFileRetention_DeletesFilesOlderThanCutoff()
    {
        var old = CreateLogFile("logs_old.json", 10, DateTime.UtcNow.AddDays(-31));
        var recent = CreateLogFile("logs_recent.json", 10, DateTime.UtcNow.AddDays(-1));

        var (deleted, failed) = LogFileRetention.Apply(_logsDirectory, DateTime.UtcNow.AddDays(-30), long.MaxValue);

        Assert.Equal(1, deleted);
        Assert.Equal(0, failed);
        Assert.False(File.Exists(old));
        Assert.True(File.Exists(recent));
    }

    [Fact]
    public void LogFileRetention_OverBudget_DeletesOldestFirstAndKeepsNewest()
    {
        var oldest = CreateLogFile("logs_a.json", 100, DateTime.UtcNow.AddHours(-3));
        var middle = CreateLogFile("logs_b.json", 100, DateTime.UtcNow.AddHours(-2));
        var newest = CreateLogFile("logs_c.json", 100, DateTime.UtcNow.AddHours(-1));

        var (deleted, _) = LogFileRetention.Apply(_logsDirectory, DateTime.UtcNow.AddDays(-30), 250);

        Assert.Equal(1, deleted);
        Assert.False(File.Exists(oldest));
        Assert.True(File.Exists(middle));
        Assert.True(File.Exists(newest));
    }

    [Fact]
    public void LogFileRetention_NewestFileLargerThanBudget_IsKept()
    {
        var newest = CreateLogFile("logs_big.json", 500, DateTime.UtcNow);

        var (deleted, _) = LogFileRetention.Apply(_logsDirectory, DateTime.UtcNow.AddDays(-30), 100);

        Assert.Equal(0, deleted);
        Assert.True(File.Exists(newest));
    }

    [Fact]
    public void LogFileRetention_IgnoresFilesNotMatchingPattern()
    {
        var other = CreateLogFile("notes.txt", 10, DateTime.UtcNow.AddDays(-90));

        var (deleted, _) = LogFileRetention.Apply(_logsDirectory, DateTime.UtcNow.AddDays(-30), long.MaxValue);

        Assert.Equal(0, deleted);
        Assert.True(File.Exists(other));
    }

    [Fact]
    public void LogFileRetention_MissingDirectory_ReturnsZero()
    {
        var (deleted, failed) = LogFileRetention.Apply(
            Path.Combine(_logsDirectory, "missing"), DateTime.UtcNow, long.MaxValue);

        Assert.Equal(0, deleted);
        Assert.Equal(0, failed);
    }

    private async Task AddLogAsync(DateTime timestampUtc, CancellationToken ct)
    {
        _context.TelemetryLogs.Add(new TelemetryLog
        {
            TimestampUtc = timestampUtc,
            Level = "Information",
            Category = "Test",
            Message = "Log"
        });
        await _context.SaveChangesAsync(ct);
    }

    private string CreateLogFile(string name, int bytes, DateTime lastWriteUtc)
    {
        var path = Path.Combine(_logsDirectory, name);
        File.WriteAllBytes(path, new byte[bytes]);
        File.SetLastWriteTimeUtc(path, lastWriteUtc);
        return path;
    }
}
