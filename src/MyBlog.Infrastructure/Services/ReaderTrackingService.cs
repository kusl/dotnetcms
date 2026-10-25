using System.Collections.Concurrent;
using MyBlog.Core.Interfaces;

namespace MyBlog.Infrastructure.Services;

public class ReaderTrackingService : IReaderTrackingService
{
    private readonly ConcurrentDictionary<string, int> _slugCounts = new();

    private readonly ConcurrentDictionary<string, string> _connectionMap = new();

    public int JoinPost(string slug, string connectionId)
    {
        _connectionMap.AddOrUpdate(connectionId, slug, (_, _) => slug);

        return _slugCounts.AddOrUpdate(slug, 1, (_, count) => count + 1);
    }

    public int LeavePost(string slug, string connectionId)
    {
        _connectionMap.TryRemove(connectionId, out _);

        return _slugCounts.AddOrUpdate(slug, 0, (_, count) => count > 0 ? count - 1 : 0);
    }

    public (string? Slug, int NewCount) Disconnect(string connectionId)
    {
        if (_connectionMap.TryRemove(connectionId, out var slug))
        {
            var newCount = _slugCounts.AddOrUpdate(slug, 0, (_, count) => count > 0 ? count - 1 : 0);
            return (slug, newCount);
        }

        return (null, 0);
    }

    public int GetReaderCount(string slug) => _slugCounts.TryGetValue(slug, out var count) ? count : 0;
}
