using NoMercy.Events;
using NoMercy.Events.Users;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Api.EventHandlers;

public class UserPermissionsEventHandler : IDisposable
{
    private readonly List<IDisposable> _subscriptions = [];

    public UserPermissionsEventHandler(IEventBus eventBus)
    {
        _subscriptions.Add(eventBus.Subscribe<UserPermissionsChangedEvent>(OnUserPermissionsChanged));
    }

    internal Task OnUserPermissionsChanged(UserPermissionsChangedEvent @event, CancellationToken ct)
    {
        Networking.Networking.SendToAll("RefreshPermissions", "dashboardHub", new
        {
            userId = @event.UserId,
            changedBy = @event.ChangedBy
        });

        Logger.Socket($"User permissions changed: UserId={@event.UserId}, ChangedBy={@event.ChangedBy}");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        foreach (IDisposable subscription in _subscriptions)
        {
            subscription.Dispose();
        }
        _subscriptions.Clear();
    }
}
