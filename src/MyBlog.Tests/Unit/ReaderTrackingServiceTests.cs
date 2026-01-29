using MyBlog.Infrastructure.Services;
using Xunit;

namespace MyBlog.Tests.Unit;

/// <summary>
/// Unit tests for the ReaderTrackingService.
/// Verifies thread-safe reader counting with ConcurrentDictionary operations.
/// </summary>
public class ReaderTrackingServiceTests
{
    private readonly ReaderTrackingService _sut = new();

    [Fact]
    public void JoinPost_WithNewSlug_ReturnsOne()
    {
        var count = _sut.JoinPost("test-slug", "connection-1");

        Assert.Equal(1, count);
    }

    [Fact]
    public void JoinPost_WithMultipleConnections_ReturnsIncrementingCount()
    {
        var count1 = _sut.JoinPost("test-slug", "connection-1");
        var count2 = _sut.JoinPost("test-slug", "connection-2");
        var count3 = _sut.JoinPost("test-slug", "connection-3");

        Assert.Equal(1, count1);
        Assert.Equal(2, count2);
        Assert.Equal(3, count3);
    }

    [Fact]
    public void JoinPost_WithDifferentSlugs_TracksIndependently()
    {
        var count1 = _sut.JoinPost("slug-a", "connection-1");
        var count2 = _sut.JoinPost("slug-b", "connection-2");
        var count3 = _sut.JoinPost("slug-a", "connection-3");

        Assert.Equal(1, count1);
        Assert.Equal(1, count2);
        Assert.Equal(2, count3);
    }

    [Fact]
    public void LeavePost_WithExistingConnection_DecrementsCount()
    {
        _sut.JoinPost("test-slug", "connection-1");
        _sut.JoinPost("test-slug", "connection-2");

        var count = _sut.LeavePost("test-slug", "connection-2");

        Assert.Equal(1, count);
    }

    [Fact]
    public void LeavePost_WhenCountIsZero_ReturnsZero()
    {
        _sut.JoinPost("test-slug", "connection-1");
        _sut.LeavePost("test-slug", "connection-1");

        var count = _sut.LeavePost("test-slug", "connection-1");

        Assert.Equal(0, count);
    }

    [Fact]
    public void LeavePost_WithNonExistentSlug_ReturnsZero()
    {
        var count = _sut.LeavePost("non-existent", "connection-1");

        Assert.Equal(0, count);
    }

    [Fact]
    public void Disconnect_WithKnownConnection_ReturnsSlugAndDecrementedCount()
    {
        _sut.JoinPost("test-slug", "connection-1");
        _sut.JoinPost("test-slug", "connection-2");

        var (slug, newCount) = _sut.Disconnect("connection-1");

        Assert.Equal("test-slug", slug);
        Assert.Equal(1, newCount);
    }

    [Fact]
    public void Disconnect_WithUnknownConnection_ReturnsNullSlug()
    {
        var (slug, newCount) = _sut.Disconnect("unknown-connection");

        Assert.Null(slug);
        Assert.Equal(0, newCount);
    }

    [Fact]
    public void Disconnect_RemovesConnectionMapping()
    {
        _sut.JoinPost("test-slug", "connection-1");
        _sut.Disconnect("connection-1");

        // Second disconnect should return null since connection is no longer tracked
        var (slug, _) = _sut.Disconnect("connection-1");

        Assert.Null(slug);
    }

    [Fact]
    public void GetReaderCount_WithExistingSlug_ReturnsCorrectCount()
    {
        _sut.JoinPost("test-slug", "connection-1");
        _sut.JoinPost("test-slug", "connection-2");

        var count = _sut.GetReaderCount("test-slug");

        Assert.Equal(2, count);
    }

    [Fact]
    public void GetReaderCount_WithNonExistentSlug_ReturnsZero()
    {
        var count = _sut.GetReaderCount("non-existent");

        Assert.Equal(0, count);
    }

    [Fact]
    public void JoinPost_SameConnectionSwitchingPages_UpdatesMapping()
    {
        // Connection joins slug-a
        _sut.JoinPost("slug-a", "connection-1");

        // Same connection joins slug-b (switching pages)
        _sut.JoinPost("slug-b", "connection-1");

        // Disconnect should return slug-b (the most recent)
        var (slug, _) = _sut.Disconnect("connection-1");

        Assert.Equal("slug-b", slug);
    }

    [Fact]
    public void ConcurrentJoins_MaintainsAccurateCount()
    {
        const int connectionCount = 100;
        var tasks = new List<Task>();

        for (var i = 0; i < connectionCount; i++)
        {
            var connectionId = $"connection-{i}";
            tasks.Add(Task.Run(() => _sut.JoinPost("test-slug", connectionId)));
        }

        Task.WaitAll(tasks.ToArray());

        var finalCount = _sut.GetReaderCount("test-slug");
        Assert.Equal(connectionCount, finalCount);
    }

    [Fact]
    public void ConcurrentJoinsAndLeaves_MaintainsAccurateCount()
    {
        // First, add 50 connections
        for (var i = 0; i < 50; i++)
        {
            _sut.JoinPost("test-slug", $"connection-{i}");
        }

        var tasks = new List<Task>();

        // Concurrently add 25 more and remove 25
        for (var i = 50; i < 75; i++)
        {
            var connectionId = $"connection-{i}";
            tasks.Add(Task.Run(() => _sut.JoinPost("test-slug", connectionId)));
        }

        for (var i = 0; i < 25; i++)
        {
            var connectionId = $"connection-{i}";
            tasks.Add(Task.Run(() => _sut.LeavePost("test-slug", connectionId)));
        }

        Task.WaitAll(tasks.ToArray());

        // Should have 50 - 25 + 25 = 50 connections
        var finalCount = _sut.GetReaderCount("test-slug");
        Assert.Equal(50, finalCount);
    }

    [Fact]
    public void ConcurrentDisconnects_MaintainsAccurateCount()
    {
        const int connectionCount = 50;

        // Add connections
        for (var i = 0; i < connectionCount; i++)
        {
            _sut.JoinPost("test-slug", $"connection-{i}");
        }

        // Disconnect all concurrently
        var tasks = new List<Task>();
        for (var i = 0; i < connectionCount; i++)
        {
            var connectionId = $"connection-{i}";
            tasks.Add(Task.Run(() => _sut.Disconnect(connectionId)));
        }

        Task.WaitAll(tasks.ToArray());

        var finalCount = _sut.GetReaderCount("test-slug");
        Assert.Equal(0, finalCount);
    }
}
