using NoMercy.Events;
using NoMercy.Events.DriveMonitor;
using NoMercy.Networking.Messaging;

namespace NoMercy.Api.EventHandlers;

public class DriveMonitorEventHandler : IDisposable
{
    private readonly IClientMessenger _clientMessenger;
    private readonly List<IDisposable> _subscriptions = [];

    public DriveMonitorEventHandler(IEventBus eventBus, IClientMessenger clientMessenger)
    {
        _clientMessenger = clientMessenger;
        _subscriptions.Add(eventBus.Subscribe<DriveStateChangedEvent>(OnDriveStateChanged));
    }

    internal Task OnDriveStateChanged(DriveStateChangedEvent @event, CancellationToken ct)
    {
        _clientMessenger.SendToAll("DriveState", "ripperHub", @event.DriveStateData);
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
