using Microsoft.EntityFrameworkCore;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;
using MyBlog.Infrastructure.Repositories;
using Xunit;

namespace MyBlog.Tests.Integration;

/// <summary>
/// Integration tests for the TelemetryLogRepository.
/// Covers WriteAsync and GetRecentAsync methods not covered by TelemetryCleanupTests.
/// Uses in-memory SQLite for cross-platform compatibility.
/// </summary>
public class TelemetryLogRepositoryTests : IAsyncDisposable
{
    private readonly BlogDbContext _context;
    private readonly TelemetryLogRepository _sut;

    public TelemetryLogRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<BlogDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _context = new BlogDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _sut = new TelemetryLogRepository(_context);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task WriteAsync_AddsLogToDatabase()
    {
        var ct = TestContext.Current.CancellationToken;
        var log = CreateTestLog("Test message");

        await _sut.WriteAsync(log, ct);

        var saved = await _context.TelemetryLogs.FindAsync([log.Id], ct);
        Assert.NotNull(saved);
        Assert.Equal("Test message", saved.Message);
    }

    [Fact]
    public async Task WriteAsync_GeneratesId()
    {
        var ct = TestContext.Current.CancellationToken;
        var log = CreateTestLog("Auto ID test");
        log.Id = 0;

        await _sut.WriteAsync(log, ct);

        Assert.NotEqual(0, log.Id);
    }

    [Fact]
    public async Task WriteAsync_PreservesAllFields()
    {
        var ct = TestContext.Current.CancellationToken;
        var timestamp = DateTime.UtcNow.AddMinutes(-5);
        var log = new TelemetryLog
        {
            TimestampUtc = timestamp,
            Level = "Warning",
            Category = "MyBlog.Tests",
            Message = "Full fields test",
            Exception = "System.Exception: Test",
            TraceId = "trace-123",
            SpanId = "span-456",
            Properties = "{\"key\":\"value\"}"
        };

        await _sut.WriteAsync(log, ct);

        var saved = await _context.TelemetryLogs.FindAsync([log.Id], ct);
        Assert.NotNull(saved);
        Assert.Equal(timestamp, saved.TimestampUtc);
        Assert.Equal("Warning", saved.Level);
        Assert.Equal("MyBlog.Tests", saved.Category);
        Assert.Equal("Full fields test", saved.Message);
        Assert.Equal("System.Exception: Test", saved.Exception);
        Assert.Equal("trace-123", saved.TraceId);
        Assert.Equal("span-456", saved.SpanId);
        Assert.Equal("{\"key\":\"value\"}", saved.Properties);
    }

    [Fact]
    public async Task GetRecentAsync_ReturnsSpecifiedCount()
    {
        var ct = TestContext.Current.CancellationToken;

        // Add 5 logs
        for (var i = 0; i < 5; i++)
        {
            await _sut.WriteAsync(CreateTestLog($"Log {i}"), ct);
        }

        var result = await _sut.GetRecentAsync(3, ct);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetRecentAsync_ReturnsNewestFirst()
    {
        var ct = TestContext.Current.CancellationToken;

        var oldLog = CreateTestLog("Old log");
        oldLog.TimestampUtc = DateTime.UtcNow.AddHours(-2);
        await _sut.WriteAsync(oldLog, ct);

        var middleLog = CreateTestLog("Middle log");
        middleLog.TimestampUtc = DateTime.UtcNow.AddHours(-1);
        await _sut.WriteAsync(middleLog, ct);

        var newLog = CreateTestLog("New log");
        newLog.TimestampUtc = DateTime.UtcNow;
        await _sut.WriteAsync(newLog, ct);

        var result = await _sut.GetRecentAsync(3, ct);

        Assert.Equal("New log", result[0].Message);
        Assert.Equal("Middle log", result[1].Message);
        Assert.Equal("Old log", result[2].Message);
    }

    [Fact]
    public async Task GetRecentAsync_WithFewerLogsThanRequested_ReturnsAllLogs()
    {
        var ct = TestContext.Current.CancellationToken;

        await _sut.WriteAsync(CreateTestLog("Only log"), ct);

        var result = await _sut.GetRecentAsync(10, ct);

        Assert.Single(result);
    }

    [Fact]
    public async Task GetRecentAsync_WithNoLogs_ReturnsEmptyList()
    {
        var ct = TestContext.Current.CancellationToken;

        var result = await _sut.GetRecentAsync(10, ct);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetRecentAsync_DoesNotTrackEntities()
    {
        var ct = TestContext.Current.CancellationToken;
        await _sut.WriteAsync(CreateTestLog("Tracking test"), ct);

        var result = await _sut.GetRecentAsync(1, ct);

        Assert.Single(result);
        var entry = _context.Entry(result[0]);
        Assert.Equal(EntityState.Detached, entry.State);
    }

    [Fact]
    public async Task WriteAsync_AllowsNullOptionalFields()
    {
        var ct = TestContext.Current.CancellationToken;
        var log = new TelemetryLog
        {
            TimestampUtc = DateTime.UtcNow,
            Level = "Information",
            Category = "Test",
            Message = "Minimal log",
            Exception = null,
            TraceId = null,
            SpanId = null,
            Properties = null
        };

        await _sut.WriteAsync(log, ct);

        var saved = await _context.TelemetryLogs.FindAsync([log.Id], ct);
        Assert.NotNull(saved);
        Assert.Null(saved.Exception);
        Assert.Null(saved.TraceId);
        Assert.Null(saved.SpanId);
        Assert.Null(saved.Properties);
    }

    [Fact]
    public async Task GetRecentAsync_WithDifferentLevels_ReturnsAllLevels()
    {
        var ct = TestContext.Current.CancellationToken;

        var infoLog = CreateTestLog("Info");
        infoLog.Level = "Information";
        await _sut.WriteAsync(infoLog, ct);

        var warnLog = CreateTestLog("Warning");
        warnLog.Level = "Warning";
        await _sut.WriteAsync(warnLog, ct);

        var errorLog = CreateTestLog("Error");
        errorLog.Level = "Error";
        await _sut.WriteAsync(errorLog, ct);

        var result = await _sut.GetRecentAsync(10, ct);

        Assert.Equal(3, result.Count);
        Assert.Contains(result, l => l.Level == "Information");
        Assert.Contains(result, l => l.Level == "Warning");
        Assert.Contains(result, l => l.Level == "Error");
    }

    private static TelemetryLog CreateTestLog(string message)
    {
        return new TelemetryLog
        {
            TimestampUtc = DateTime.UtcNow,
            Level = "Information",
            Category = "MyBlog.Tests",
            Message = message
        };
    }
}
