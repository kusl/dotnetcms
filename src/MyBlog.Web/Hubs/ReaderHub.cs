using Microsoft.AspNetCore.SignalR;
using MyBlog.Core.Interfaces;

namespace MyBlog.Web.Hubs;

public class ReaderHub : Hub
{
    private readonly IReaderTrackingService _trackingService;

    public ReaderHub(IReaderTrackingService trackingService)
    {
        _trackingService = trackingService;
    }

    public async Task JoinPage(string slug)
    {
        // Add this connection to the SignalR group for this slug
        await Groups.AddToGroupAsync(Context.ConnectionId, slug);

        // Update state
        var newCount = _trackingService.JoinPost(slug, Context.ConnectionId);

        // Broadcast new count to everyone in this group
        await Clients.Group(slug).SendAsync("UpdateCount", newCount);
    }

    public async Task LeavePage(string slug)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, slug);

        var newCount = _trackingService.LeavePost(slug, Context.ConnectionId);

        await Clients.Group(slug).SendAsync("UpdateCount", newCount);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Handle abrupt disconnects (tab closed, network lost)
        var (slug, newCount) = _trackingService.Disconnect(Context.ConnectionId);

        if (!string.IsNullOrEmpty(slug))
        {
            await Clients.Group(slug).SendAsync("UpdateCount", newCount);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
