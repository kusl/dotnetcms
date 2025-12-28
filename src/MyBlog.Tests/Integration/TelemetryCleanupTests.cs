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
        var ct = TestContext.Current.CancellationToken;
        
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
        await _context.SaveChangesAsync(ct);

        var cutoff = DateTime.UtcNow.AddDays(-30);
        var deleted = await _sut.DeleteOlderThanAsync(cutoff, ct);

        Assert.Equal(1, deleted);
        var remaining = await _context.TelemetryLogs.CountAsync(ct);
        Assert.Equal(1, remaining);
    }

    [Fact]
    public async Task DeleteOlderThanAsync_WithNoOldLogs_ReturnsZero()
    {
        var ct = TestContext.Current.CancellationToken;
        
        var recentLog = new TelemetryLog
        {
            TimestampUtc = DateTime.UtcNow.AddDays(-5),
            Level = "Information",
            Category = "Test",
            Message = "Recent log"
        };
        _context.TelemetryLogs.Add(recentLog);
        await _context.SaveChangesAsync(ct);

        var cutoff = DateTime.UtcNow.AddDays(-30);
        var deleted = await _sut.DeleteOlderThanAsync(cutoff, ct);

        Assert.Equal(0, deleted);
    }

    [Fact]
    public async Task DeleteOlderThanAsync_WithEmptyTable_ReturnsZero()
    {
        var ct = TestContext.Current.CancellationToken;
        var cutoff = DateTime.UtcNow.AddDays(-30);
        var deleted = await _sut.DeleteOlderThanAsync(cutoff, ct);
        Assert.Equal(0, deleted);
    }
}
