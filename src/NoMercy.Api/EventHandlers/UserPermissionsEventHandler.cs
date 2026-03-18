using NoMercy.Events;
using NoMercy.Events.Users;
using NoMercy.Networking.Messaging;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Api.EventHandlers;

public class UserPermissionsEventHandler : IDisposable
{
    private readonly IClientMessenger _clientMessenger;
    private readonly List<IDisposable> _subscriptions = [];

    public UserPermissionsEventHandler(IEventBus eventBus, IClientMessenger clientMessenger)
    {
        _clientMessenger = clientMessenger;
        _subscriptions.Add(eventBus.Subscribe<UserPermissionsChangedEvent>(OnUserPermissionsChanged));
    }

    internal Task OnUserPermissionsChanged(UserPermissionsChangedEvent @event, CancellationToken ct)
    {
        _clientMessenger.SendToAll("RefreshPermissions", "dashboardHub", new
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
