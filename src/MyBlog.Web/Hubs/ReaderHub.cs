using Microsoft.AspNetCore.SignalR;
using MyBlog.Core.Interfaces;

namespace MyBlog.Web.Hubs;

public class ReaderHub(IReaderTrackingService trackingService) : Hub
{
    public async Task JoinPage(string slug)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, slug);

        var newCount = trackingService.JoinPost(slug, Context.ConnectionId);

        await Clients.Group(slug).SendAsync("UpdateCount", newCount);
    }

    public async Task LeavePage(string slug)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, slug);

        var newCount = trackingService.LeavePost(slug, Context.ConnectionId);

        await Clients.Group(slug).SendAsync("UpdateCount", newCount);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var (slug, newCount) = trackingService.Disconnect(Context.ConnectionId);

        if (!string.IsNullOrEmpty(slug))
        {
            await Clients.Group(slug).SendAsync("UpdateCount", newCount);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
