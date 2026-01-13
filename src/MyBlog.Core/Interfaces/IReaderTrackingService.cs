namespace MyBlog.Core.Interfaces;

public interface IReaderTrackingService
{
    // Called when a user opens a post
    void JoinPost(string slug);

    // Called when a user leaves (closes tab/navigates away)
    void LeavePost(string slug);

    // Gets the current count
    int GetReaderCount(string slug);

    // Event that fires when the count changes
    event Action<string, int>? OnCountChanged;
}
