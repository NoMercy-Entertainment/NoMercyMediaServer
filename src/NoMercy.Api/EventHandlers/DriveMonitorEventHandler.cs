using NoMercy.Events;
using NoMercy.Events.DriveMonitor;

namespace NoMercy.Api.EventHandlers;

public class DriveMonitorEventHandler : IDisposable
{
    private readonly List<IDisposable> _subscriptions = [];

    public DriveMonitorEventHandler(IEventBus eventBus)
    {
        _subscriptions.Add(eventBus.Subscribe<DriveStateChangedEvent>(OnDriveStateChanged));
    }

    internal Task OnDriveStateChanged(DriveStateChangedEvent @event, CancellationToken ct)
    {
        Networking.Networking.SendToAll("DriveState", "ripperHub", @event.DriveStateData);
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
