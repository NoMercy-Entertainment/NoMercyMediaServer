using Microsoft.AspNetCore.SignalR;
using NoMercy.Data.Activity;
using NoMercy.Database.Models.Users;

namespace NoMercy.Api.Hubs;

public class ActivityHubBroadcaster : IActivityHubBroadcaster
{
    private const string ModeratorsGroup = "moderators";
    private const string EventName = "ActivityLogged";

    private readonly IHubContext<DashboardHub> _hubContext;

    public ActivityHubBroadcaster(IHubContext<DashboardHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task BroadcastAsync(ActivityLog row, CancellationToken ct = default)
    {
        return _hubContext.Clients.Group(ModeratorsGroup).SendAsync(EventName, row, ct);
    }
}
