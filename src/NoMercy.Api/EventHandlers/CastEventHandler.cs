using NoMercy.Events;
using NoMercy.Events.Cast;
using NoMercy.Networking.Messaging;

namespace NoMercy.Api.EventHandlers;

public class CastEventHandler : IDisposable
{
    private readonly IClientMessenger _clientMessenger;
    private readonly List<IDisposable> _subscriptions = [];

    public CastEventHandler(IEventBus eventBus, IClientMessenger clientMessenger)
    {
        _clientMessenger = clientMessenger;
        _subscriptions.Add(
            eventBus.Subscribe<CastDeviceStatusChangedEvent>(OnCastDeviceStatusChanged)
        );
    }

    internal async Task OnCastDeviceStatusChanged(
        CastDeviceStatusChangedEvent @event,
        CancellationToken ct
    )
    {
        await _clientMessenger.SendToAll(@event.EventType, "castHub", @event.StatusData);
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
