using Microsoft.EntityFrameworkCore;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;
using MyBlog.Infrastructure.Repositories;
using Xunit;

namespace MyBlog.Tests.Integration;

public class TelemetryCleanupTests : IAsyncDisposable
{
    private readonly BlogDbContext _context;
    private readonly TelemetryLogRepository _sut;

    public TelemetryCleanupTests()
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
    public async Task DeleteOlderThanAsync_RemovesOldLogs()
    {
        // Add old logs
        var oldLog = new TelemetryLog
        {
            TimestampUtc = DateTime.UtcNow.AddDays(-60),
            Level = "Information",
            Category = "Test",
            Message = "Old log"
        };
        _context.TelemetryLogs.Add(oldLog);

        // Add recent log
        var recentLog = new TelemetryLog
        {
            TimestampUtc = DateTime.UtcNow.AddDays(-5),
            Level = "Information",
            Category = "Test",
            Message = "Recent log"
        };
        _context.TelemetryLogs.Add(recentLog);
        await _context.SaveChangesAsync();

        var cutoff = DateTime.UtcNow.AddDays(-30);
        var deleted = await _sut.DeleteOlderThanAsync(cutoff);

        Assert.Equal(1, deleted);
        var remaining = await _context.TelemetryLogs.CountAsync();
        Assert.Equal(1, remaining);
    }

    [Fact]
    public async Task DeleteOlderThanAsync_KeepsRecentLogs()
    {
        var recentLog = new TelemetryLog
        {
            TimestampUtc = DateTime.UtcNow.AddDays(-1),
            Level = "Information",
            Category = "Test",
            Message = "Recent log"
        };
        _context.TelemetryLogs.Add(recentLog);
        await _context.SaveChangesAsync();

        var cutoff = DateTime.UtcNow.AddDays(-30);
        var deleted = await _sut.DeleteOlderThanAsync(cutoff);

        Assert.Equal(0, deleted);
        var remaining = await _context.TelemetryLogs.CountAsync();
        Assert.Equal(1, remaining);
    }

    [Fact]
    public async Task WriteAsync_AddsLogToDatabase()
    {
        var log = new TelemetryLog
        {
            TimestampUtc = DateTime.UtcNow,
            Level = "Error",
            Category = "Test",
            Message = "Test error message"
        };

        await _sut.WriteAsync(log);

        var saved = await _context.TelemetryLogs.FirstOrDefaultAsync();
        Assert.NotNull(saved);
        Assert.Equal("Error", saved.Level);
        Assert.Equal("Test error message", saved.Message);
    }
}
