using MyBlog.Core.Interfaces;

namespace MyBlog.Infrastructure.Services;

public sealed class ReaderTrackingService : IReaderTrackingService
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, List<Reader>> _readersBySlug = new(StringComparer.Ordinal);

    public IDisposable Track(string slug, Action onReadersChanged)
    {
        ArgumentException.ThrowIfNullOrEmpty(slug);
        ArgumentNullException.ThrowIfNull(onReadersChanged);

        var reader = new Reader(this, slug, onReadersChanged);
        Reader[] audience;

        lock (_gate)
        {
            if (!_readersBySlug.TryGetValue(slug, out var readers))
            {
                readers = [];
                _readersBySlug[slug] = readers;
            }

            readers.Add(reader);
            audience = [.. readers];
        }

        Notify(audience);
        return reader;
    }

    public int GetReaderCount(string slug)
    {
        lock (_gate)
        {
            return _readersBySlug.TryGetValue(slug, out var readers) ? readers.Count : 0;
        }
    }

    private void Release(Reader reader)
    {
        Reader[] audience;

        lock (_gate)
        {
            if (!_readersBySlug.TryGetValue(reader.Slug, out var readers) || !readers.Remove(reader))
            {
                return;
            }

            if (readers.Count == 0)
            {
                _readersBySlug.Remove(reader.Slug);
            }

            audience = [.. readers];
        }

        Notify(audience);
    }

    private static void Notify(Reader[] audience)
    {
        foreach (var reader in audience)
        {
            reader.Notify();
        }
    }

    private sealed class Reader(ReaderTrackingService owner, string slug, Action onReadersChanged) : IDisposable
    {
        private int _released;

        public string Slug => slug;

        public void Notify() => onReadersChanged();

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                owner.Release(this);
            }
        }
    }
}
