using System.Collections.Concurrent;
using MyBlog.Core.Interfaces;

namespace MyBlog.Infrastructure.Services;

public class ReaderTrackingService : IReaderTrackingService
{
    // Maps Slug -> Count of active readers
    private readonly ConcurrentDictionary<string, int> _slugCounts = new();

    // Maps ConnectionId -> Slug (Reverse lookup to handle disconnects)
    private readonly ConcurrentDictionary<string, string> _connectionMap = new();

    public int JoinPost(string slug, string connectionId)
    {
        // Map the connection to the slug
        _connectionMap.AddOrUpdate(connectionId, slug, (_, _) => slug);

        // Increment the count for this slug
        return _slugCounts.AddOrUpdate(slug, 1, (_, count) => count + 1);
    }

    public int LeavePost(string slug, string connectionId)
    {
        // Remove the connection mapping
        _connectionMap.TryRemove(connectionId, out _);

        // Decrement the count
        return _slugCounts.AddOrUpdate(slug, 0, (_, count) => count > 0 ? count - 1 : 0);
    }

    public (string? Slug, int NewCount) Disconnect(string connectionId)
    {
        // Find which slug this connection was watching
        if (_connectionMap.TryRemove(connectionId, out var slug))
        {
            // Decrement that slug's count
            var newCount = _slugCounts.AddOrUpdate(slug, 0, (_, count) => count > 0 ? count - 1 : 0);
            return (slug, newCount);
        }

        return (null, 0);
    }

    public int GetReaderCount(string slug)
    {
        return _slugCounts.TryGetValue(slug, out var count) ? count : 0;
    }
}
