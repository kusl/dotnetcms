using System.Collections.Concurrent;
using MyBlog.Core.Interfaces;

namespace MyBlog.Infrastructure.Services;

public class ReaderTrackingService : IReaderTrackingService
{
    // Thread-safe dictionary to store counts: Slug -> Count
    private readonly ConcurrentDictionary<string, int> _activeReaders = new();

    public event Action<string, int>? OnCountChanged;

    public void JoinPost(string slug)
    {
        // Atomically increment the count
        var newCount = _activeReaders.AddOrUpdate(slug, 1, (_, count) => count + 1);

        // Notify subscribers
        OnCountChanged?.Invoke(slug, newCount);
    }

    public void LeavePost(string slug)
    {
        // Atomically decrement the count
        var newCount = _activeReaders.AddOrUpdate(slug, 0, (_, count) => count > 0 ? count - 1 : 0);

        // If count is 0, we could remove the key, but keeping it is harmless for small blogs
        if (newCount == 0)
        {
            _activeReaders.TryRemove(slug, out _);
        }

        // Notify subscribers
        OnCountChanged?.Invoke(slug, newCount);
    }

    public int GetReaderCount(string slug)
    {
        return _activeReaders.TryGetValue(slug, out var count) ? count : 0;
    }
}
