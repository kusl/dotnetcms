namespace MyBlog.Core.Interfaces;

public interface IReaderTrackingService
{
    /// <summary>
    /// Registers a connection viewing a specific post.
    /// </summary>
    /// <returns>The new reader count for this slug.</returns>
    int JoinPost(string slug, string connectionId);

    /// <summary>
    /// Unregisters a connection from a specific post.
    /// </summary>
    /// <returns>The new reader count for this slug.</returns>
    int LeavePost(string slug, string connectionId);

    /// <summary>
    /// Handles a disconnection event (e.g. tab closed) and determines which slug was being viewed.
    /// </summary>
    /// <returns>A tuple containing the Slug that was left (if any) and the new count.</returns>
    (string? Slug, int NewCount) Disconnect(string connectionId);

    /// <summary>
    /// Gets the current count for a specific post.
    /// </summary>
    int GetReaderCount(string slug);
}
