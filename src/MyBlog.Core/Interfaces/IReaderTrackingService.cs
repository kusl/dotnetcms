namespace MyBlog.Core.Interfaces;

public interface IReaderTrackingService
{
    IDisposable Track(string slug, Action onReadersChanged);

    int GetReaderCount(string slug);
}
