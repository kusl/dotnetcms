namespace MyBlog.Core.Interfaces;

public interface IReaderTrackingService
{
    int JoinPost(string slug, string connectionId);

    int LeavePost(string slug, string connectionId);

    (string? Slug, int NewCount) Disconnect(string connectionId);

    int GetReaderCount(string slug);
}
