using NoMercy.Events;
using NoMercy.Events.Cast;

namespace NoMercy.Api.EventHandlers;

public class CastEventHandler : IDisposable
{
    private readonly List<IDisposable> _subscriptions = [];

    public CastEventHandler(IEventBus eventBus)
    {
        _subscriptions.Add(eventBus.Subscribe<CastDeviceStatusChangedEvent>(OnCastDeviceStatusChanged));
    }

    internal Task OnCastDeviceStatusChanged(CastDeviceStatusChangedEvent @event, CancellationToken ct)
    {
        Networking.Networking.SendToAll(@event.EventType, "castHub", @event.StatusData);
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
